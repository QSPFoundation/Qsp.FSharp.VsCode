namespace Ionide.VSCode.FSharp

open System
open Fable.Core
open Fable.Core.JsInterop
open Fable.Import
open Fable.Import.vscode
open Ionide.VSCode.Helpers

open DTO
open LanguageServer
open Thoth.Json

module Notifications =
    type DocumentParsedEvent =
        { fileName : string
          version : float
          /// BEWARE: Live object, might have changed since the parsing
          document : TextDocument }


    let onDocumentParsedEmitter = EventEmitter<DocumentParsedEvent>()
    let onDocumentParsed = onDocumentParsedEmitter.event

    let private tooltipRequestedEmitter = EventEmitter<Position>()
    let tooltipRequested = tooltipRequestedEmitter.event

    let mutable notifyWorkspaceHandler : Option<Choice<ProjectResult,ProjectLoadingResult,(string * ErrorData),string> -> unit> = None

module Promise =
    let inline empty<'T when 'T : null> =
        // Promise.lift (Microsoft.FSharp.Core.Result.Ok ())
        Promise.lift (null : 'T)

module LanguageService =
    module Types =
        type PlainNotification= { content: string }

        type ConfigValue<'a> =
        | UserSpecified of 'a
        | Implied of 'a

        type [<RequireQualifiedAccess>] FSACTargetRuntime =
        | NET
        | NetcoreFdd

        /// Position in a text document expressed as zero-based line and zero-based character offset.
        /// A position is between two characters like an ‘insert’ cursor in a editor.
        type Position = {
            /// Line position in a document (zero-based).
            Line: int

            /// Character offset on a line in a document (zero-based). Assuming that the line is
            /// represented as a string, the `character` value represents the gap between the
            /// `character` and `character + 1`.
            ///
            /// If the character value is greater than the line length it defaults back to the
            /// line length.
            Character: int
        }

        type DocumentUri = string

        type TextDocumentIdentifier = {Uri: DocumentUri }

        type TextDocumentPositionParams = {
            TextDocument: TextDocumentIdentifier
            Position: Position
        }

        type FileParams = {
            Project: TextDocumentIdentifier
        }

        type WorkspaceLoadParms = {
            TextDocuments: TextDocumentIdentifier[]
        }

        type HighlightingRequest = {FileName : string; }


    let mutable client : LanguageClient option = None
    let mutable clientType : Types.FSACTargetRuntime = Types.FSACTargetRuntime.NetcoreFdd

    let private handleUntitled (fn : string) = if fn.EndsWith ".fs" || fn.EndsWith ".fsi" || fn.EndsWith ".fsx" then fn else (fn + ".fsx")

    let private deserializeProjectResult (res : ProjectResult) =
        let parseInfo (f : obj) =
            match f?SdkType |> unbox with
            | "dotnet/sdk" ->
                ProjectResponseInfo.DotnetSdk (f?Data |> unbox)
            | "verbose" ->
                ProjectResponseInfo.Verbose
            | "project.json" ->
                ProjectResponseInfo.ProjectJson
            | _ ->
                f |> unbox

        { res with
            Data = { res.Data with
                        Info = parseInfo(res.Data.Info) } }


    let getHighlighting (f) =
        match client with
        | None -> Promise.lift (Error "client not ready")
        | Some cl ->
            let req : Types.HighlightingRequest= {
                FileName = f
            }
            printfn "fsharp/highlighting"
            cl.sendRequest("fsharp/highlighting", req)
            |> Promise.map (fun (res: Types.PlainNotification) ->
                printfn "%s" res.content
                res.content |> Decode.Auto.fromString<HighlightingResult>
            )
            |> Promise.catch (fun e -> Error (sprintf "%A" e))

    let private createClient opts =
        let options =
            createObj [
                "run" ==> opts
                "debug" ==> opts
                ] |> unbox<ServerOptions>

        let fileDeletedWatcher = workspace.createFileSystemWatcher("**/*.{qsps,qproj}", false, false, false)

        let clientOpts =
            let opts = createEmpty<Client.LanguageClientOptions>
            let selector =
                createObj [
                    "language" ==> "qsp"
                ] |> unbox<Client.DocumentSelector>

            let initOpts =
                createObj [
                    "AutomaticWorkspaceInit" ==> false
                ]

            let synch = createEmpty<Client.SynchronizeOptions>
            synch.configurationSection <- Some !^"Qsp"
            synch.fileEvents <- Some( !^ ResizeArray([fileDeletedWatcher]))

            opts.documentSelector <- Some !^selector
            opts.synchronize <- Some synch
            opts.revealOutputChannelOn <- Some Client.RevealOutputChannelOn.Warn


            opts.initializationOptions <- Some !^(Some initOpts)

            opts

        let cl = LanguageClient("qsp", "QSP", options, clientOpts, false)
        client <- Some cl
        cl

    let getOptions () = promise {
        let dotnetNotFound () = promise {
            let msg = """
            Cannot start QSP language services because `dotnet` was not found.
            """
            let! result = vscode.window.showErrorMessage(msg)

            return failwith "no `dotnet` binary found"
        }
        if Environment.isWin then
            let fsautocompletePath =
                VSCodeExtension.ionidePluginPath () + "/bin/net461/qspserver.exe"
            printfn "%A" fsautocompletePath
            let args =
                [
                    // if backgroundSymbolCache then yield "--background-service-enabled"
                    // if verbose then yield  "--verbose"
                ] |> ResizeArray
            let spawnNetWin =
                createObj [
                    "command" ==> fsautocompletePath
                    "args" ==> args
                    "transport" ==> 0
                ]
            return spawnNetWin
        else
            let spawnServer dotnet =
                let fsautocompletePath =
                    [
                        VSCodeExtension.ionidePluginPath () + "/bin/netcoreapp3.1/qspserver.dll"
                    ] |> ResizeArray

                createObj [
                    "command" ==> dotnet
                    "args" ==> fsautocompletePath
                    "transport" ==> 0
                ]

            let! dotnet = Environment.dotnet
            match dotnet with
            | Some dotnet ->
                return spawnServer dotnet
            | None ->
                return! dotnetNotFound ()
    }

    let getFile title =
        let openDialogOptions =
            { new OpenDialogOptions with
                member __.canSelectFiles
                    with get () = Some true
                    and set x = failwith "set canSelectFiles is not implemented"
                member __.canSelectFolders
                    with get () = Some false
                    and set x = failwith "set canSelectFolders is not implemented"
                member __.canSelectMany
                    with get () = Some false
                    and set x = failwith "set canSelectMany is not implemented"
                member __.defaultUri
                    with get () = None
                    and set x = failwith "set defaultUri is not implemented"
                member __.filters
                    with get () = None
                    and set x = failwith "set filters is not implemented"
                member __.openLabel
                    with get () = Some title
                    and set x = failwith "set openLabel is not implemented"
            }
        vscode.window.showOpenDialog openDialogOptions
        |> Promise.map (fun x ->
            if isNull x then // nothing selected
                None
            else
                match Array.tryHead x with
                | Some uri ->
                    Some uri
                | None -> None // what happened here?
        )
    let buildQspGen cmd =
        let editor = vscode.window.activeTextEditor
        editor.document.save()
        |> Promise.bind (fun isSaved ->
            if isSaved then
                client
                |> Option.map (fun client ->
                    client.sendRequest(cmd, editor.document.uri.fsPath)
                    |> Promise.bind (fun res ->
                        // Ну и зачем так делать? Не проще ли было сделать какой-нибудь преобразователь с Newtonsoft.Json в Fable.Core.JsInterop и обратно? Идиотизм какой-то.
                        let case : string = res?case |> unbox
                        let fields : string [] = res?fields |> unbox

                        match case with
                        | "Choice1Of2" ->
                            vscode.window.showErrorMessage fields.[0]
                        | "Choice2Of2" ->
                            // vscode.window.showInformationMessage fields.[0]
                            Promise.empty
                        | _ ->
                            Promise.empty
                    )
                )
                |> Option.defaultValue Promise.empty
            else
                Promise.empty
        )

    let buildQsp () = buildQspGen "qsp/build"
    let buildQspAndRun () = buildQspGen "qsp/buildAndRun"

    let readyClient (ctx : ExtensionContext) (cl: LanguageClient) =
        cl.onReady ()
        |> Promise.map (fun _ ->
            let disposable =
                vscode.commands.registerCommand("extension.build",
                    buildQsp
                    |> unbox<Func<obj, obj>>
                )
            ctx.subscriptions.Add(disposable)
            let disposable =
                vscode.commands.registerCommand("extension.buildAndRun",
                    buildQspAndRun
                    |> unbox<Func<obj, obj>>
                )
            ctx.subscriptions.Add(disposable)

            // vscode.window.showInformationMessage "client is ready" |> ignore
            ()
        )

    let start (c : ExtensionContext) =
        promise {
            let! startOpts = getOptions ()
            let cl = createClient startOpts
            c.subscriptions.Add (cl.start ())
            let! _ = readyClient c cl
            return ()
        }

    let stop () =
        promise {
            match client with
            | Some cl -> return! cl.stop()
            | None -> return ()
        }
