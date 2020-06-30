﻿namespace Ionide.VSCode.FSharp

open System
open Fable.Core
open Fable.Core.JsInterop
open Fable.Import
open Fable.Import.vscode
open Fable.Import.Node
open Ionide.VSCode.Helpers

open DTO
open LanguageServer

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


    let compilerLocation () =
        match client with
        | None -> Promise.empty
        | Some cl ->
            cl.sendRequest("fsharp/compilerLocation", null)
            |> Promise.map (fun (res: Types.PlainNotification) ->
                let r = res.content |> ofJson<CompilerLocationResult>
                r
            )

    let dotnet () = promise {
        let! dotnet = Environment.dotnet
        match dotnet with
        | None ->
            let! location = compilerLocation()
            match location.Data.SdkRoot with
            | Some root ->
                if Environment.isWin
                then return Some (path.join (root, "dotnet.exe"))
                else return Some (path.join (root, "dotnet"))
            | None ->
                return None
        | Some location -> return Some location
    }

    let f1Help fn line col : JS.Promise<Result<string>> =
        match client with
        | None -> Promise.empty
        | Some cl ->
            let req : Types.TextDocumentPositionParams= {
                TextDocument = {Uri = handleUntitled fn}
                Position = {Line = line; Character = col}
            }

            cl.sendRequest("fsharp/f1Help", req )
            |> Promise.map (fun (res: Types.PlainNotification) ->
                res.content |> ofJson<Result<string>>
            )

    let documentation fn line col =
        match client with
        | None -> Promise.empty
        | Some cl ->
            let req : Types.TextDocumentPositionParams= {
                TextDocument = {Uri = handleUntitled fn}
                Position = {Line = line; Character = col}
            }

            cl.sendRequest("fsharp/documentation", req )
            |> Promise.map (fun (res: Types.PlainNotification) ->
                res.content |> ofJson<Result<DocumentationDescription[][]>>
            )

    let documentationForSymbol xmlSig assembly =
        match client with
        | None -> Promise.empty
        | Some cl ->
            let req = { DocumentationForSymbolReuqest.Assembly = assembly; XmlSig = xmlSig}

            cl.sendRequest("fsharp/documentationSymbol", req )
            |> Promise.map (fun (res: Types.PlainNotification) ->
                res.content |> ofJson<Result<DocumentationDescription[][]>>
            )

    let signature fn line col =
        match client with
        | None -> Promise.empty
        | Some cl ->
            let req : Types.TextDocumentPositionParams= {
                TextDocument = {Uri = handleUntitled fn}
                Position = {Line = line; Character = col}
            }

            cl.sendRequest("fsharp/signature", req )
            |> Promise.map (fun (res: Types.PlainNotification) ->
                res.content |> ofJson<Result<string>>
            )

    let signatureData fn line col =
        match client with
        | None -> Promise.empty
        | Some cl ->
            let req : Types.TextDocumentPositionParams= {
                TextDocument = {Uri = handleUntitled fn}
                Position = {Line = line; Character = col}
            }

            cl.sendRequest("fsharp/signatureData", req)
            |> Promise.map (fun (res: Types.PlainNotification) ->
                res.content |> ofJson<SignatureDataResult>
            )

    let generateDocumentation fn line col =
        match client with
        | None -> Promise.empty
        | Some cl ->
            let req : Types.TextDocumentPositionParams= {
                TextDocument = {Uri = handleUntitled fn}
                Position = {Line = line; Character = col}
            }

            cl.sendRequest("fsharp/documentationGenerator", req)
            |> Promise.map (fun (res: Types.PlainNotification) ->
                res.content |> ofJson<SignatureDataResult>
            )

    let lineLenses fn  =
        match client with
        | None -> Promise.empty
        | Some cl ->
            let req : Types.FileParams= {
                Project = {Uri = handleUntitled fn}
            }
            cl.sendRequest("fsharp/lineLens", req)
            |> Promise.map (fun (res: Types.PlainNotification) ->
                res.content |> ofJson<Result<Symbols[]>>
            )

    let compile s =
        match client with
        | None -> Promise.empty
        | Some cl ->
            let req : Types.FileParams= {
                Project = {Uri = handleUntitled s}
            }
            cl.sendRequest("fsharp/compile", req)
            |> Promise.map (fun (res: Types.PlainNotification) ->
                res.content |> ofJson<CompileResult>
            )

    let fsdn (signature: string) =
        let parse (ws : obj) =
            { FsdnResponse.Functions = ws?Functions |> unbox }

        match client with
        | None -> Promise.empty
        | Some cl ->
            let req : Types.FileParams= {
                Project = {Uri = signature}
            }
            cl.sendRequest("fsharp/fsdn", req)
            |> Promise.map (fun (res: Types.PlainNotification) ->
                let res = res.content |> ofJson<obj>
                parse (res?Data |> unbox)
            )

    let dotnetNewList () =
        match client with
        | None -> Promise.empty
        | Some cl ->
            let req : DotnetNew.DotnetNewListRequest= {
                Query = ""
            }

            cl.sendRequest("fsharp/dotnetnewlist", req)
            |> Promise.map (fun (res: Types.PlainNotification) ->
                let x = res.content |> ofJson<DotnetNew.DotnetNewListResponse>
                x.Data
            )
    let dotnetNewRun (template: string)  (name: string option) (output: string option) =
        match client with
        | None -> Promise.empty
        | Some cl ->
            let req : DotnetNew.DotnetNewRunRequest= {
                Template = template
                Output = output
                Name = name
            }

            cl.sendRequest("fsharp/dotnetnewrun", req)
            |> Promise.map (fun (res: Types.PlainNotification) ->
                ()
            )


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

    let parseError (err : obj) =
        let data =
            match err?Code |> unbox with
            | ErrorCodes.GenericError ->
                ErrorData.GenericError
            | ErrorCodes.ProjectNotRestored ->
                ErrorData.ProjectNotRestored (err?AdditionalData |> unbox)
            | ErrorCodes.ProjectParsingFailed ->
                ErrorData.ProjectParsingFailed (err?AdditionalData |> unbox)
            | ErrorCodes.LanguageNotSupported ->
                ErrorData.LangugageNotSupported (err?AdditionalData |> unbox)
            | unknown ->
                ErrorData.GenericError
        (err?Message |> unbox<string>), data

    let project s =
        match client with
        | None -> Promise.empty
        | Some cl ->
            let req : Types.FileParams= {
                Project = {Uri = s}
            }
            cl.sendRequest("fsharp/project", req)
            |> Promise.map (fun (res: Types.PlainNotification) ->
                let res = res.content |> ofJson<obj>
                match res?Kind |> unbox with
                | "error" ->
                    res?Data |> parseError |> Error
                | _ ->
                    deserializeProjectResult (res |> unbox) |> Ok
            )

    let workspacePeek dir deep excludedDirs =
        let rec mapItem (f : WorkspacePeekFoundSolutionItem) : WorkspacePeekFoundSolutionItem option =
            let mapItemKind (i : obj) : WorkspacePeekFoundSolutionItemKind option =
                let data = i?Data
                match i?Kind |> unbox with
                | "folder" ->
                    let folderData : WorkspacePeekFoundSolutionItemKindFolder = data |> unbox
                    let folder : WorkspacePeekFoundSolutionItemKindFolder =
                        { Files = folderData.Files
                          Items = folderData.Items |> Array.choose mapItem }
                    Some (WorkspacePeekFoundSolutionItemKind.Folder folder)
                | "msbuildFormat" ->
                    Some (WorkspacePeekFoundSolutionItemKind.MsbuildFormat (data |> unbox))
                | _ ->
                    None
            match mapItemKind f.Kind with
            | Some kind ->
                Some
                   { WorkspacePeekFoundSolutionItem.Guid = f.Guid
                     Name = f.Name
                     Kind = kind }
            | None -> None

        let mapFound (f : obj) : WorkspacePeekFound option =
            let data = f?Data
            match f?Type |> unbox with
            | "directory" ->
                Some (WorkspacePeekFound.Directory (data |> unbox))
            | "solution" ->
                let sln =
                    { WorkspacePeekFoundSolution.Path = data?Path |> unbox
                      Configurations = data?Configurations |> unbox
                      Items = data?Items |> unbox |> Array.choose mapItem }
                Some (WorkspacePeekFound.Solution sln)
            | _ ->
                None

        let parse (ws : obj) =
            { WorkspacePeek.Found = ws?Found |> unbox |> Array.choose mapFound }

        match client with
        | None -> Promise.empty
        | Some cl ->
            let req = { WorkspacePeekRequest.Directory = dir; Deep = deep; ExcludedDirs = excludedDirs |> Array.ofList }
            cl.sendRequest("fsharp/workspacePeek", req)
            |> Promise.map (fun (res: Types.PlainNotification) ->
                let res = res.content |> ofJson<obj>
                parse (res?Data |> unbox)
            )

    let workspaceLoad (projects: string list)  =
        match client with
        | None -> Promise.empty
        | Some cl ->
            let req : Types.WorkspaceLoadParms= {
                TextDocuments = projects |> List.map (fun s -> {Types.Uri = s}) |> List.toArray
            }
            cl.sendRequest("fsharp/workspaceLoad", req)
            |> Promise.map (fun (res: Types.PlainNotification) ->
                ()
            )

    let loadAnalyzers () =
        match client with
        | None -> Promise.empty
        | Some cl ->
            let req : Types.FileParams= {
                Project = {Uri = ""}
            }
            cl.sendRequest("fsharp/loadAnalyzers", req)
            |> Promise.map (ignore)

    let getHighlighting (f) =
        match client with
        | None -> Promise.empty
        | Some cl ->
            let req : Types.HighlightingRequest= {
                FileName = f
            }
            cl.sendRequest("fsharp/highlighting", req)
            |> Promise.map (fun (res: Types.PlainNotification) ->
                res.content |> ofJson<HighlightingResult>
            )


    module FakeSupport =
        open DTO.FakeSupport

        let logger = ConsoleAndOutputChannelLogger(Some "FakeTargets", Level.DEBUG, None, Some Level.DEBUG)

        let fakeRuntime () =
            match client with
            | None ->
                Promise.empty
            | Some cl ->
                cl.sendRequest("fake/runtimePath", null)
                |> Promise.map (fun (res: Types.PlainNotification) ->
                    res.content |> ofJson<Result<string>>
                )
                |> Promise.onFail (fun o ->
                    logger.Error("Error in fake/runtimePath request.", o)
                )
                |> Promise.map (fun c -> c.Data)

        let targetsInfo (fn:string) =
            match client with
            | None ->
                Promise.empty
            | Some cl ->
                dotnet ()
                |> Promise.bind (fun dotnetRuntime ->
                    match dotnetRuntime with
                    | Some r ->
                        let req = { TargetRequest.FileName = handleUntitled fn; FakeContext = { DotNetRuntime = r }}
                        cl.sendRequest("fake/listTargets", req)
                        |> Promise.map (fun (res: Types.PlainNotification) ->
                            res.content |> ofJson<GetTargetsResult>
                        )
                        |> Promise.onFail (fun o ->
                            logger.Error("Error in fake/listTargets request.", o)
                        )

                    | None ->
                        let msg = """
Cannot request fake targets because `dotnet` was not found.
Consider:
* setting the `FSharp.dotnetRoot` settings key to a directory with a `dotnet` binary,
* including `dotnet` in your PATH,
* installing .NET Core into one of the default locations, or
"""
                        logger.Error(msg)
                        Promise.reject (msg)
                    )


    let private fsacConfig () =
        compilerLocation ()
        |> Promise.map (fun c -> c.Data)

    let fsi () =
        let fileExists (path: string): JS.Promise<bool> =
            Promise.create(fun success _failure ->
                fs.access(!^path, fs.constants.F_OK, fun err -> success(err.IsNone)))

        let getAnyCpuFsiPathFromCompilerLocation (location: CompilerLocation) = promise {
            match location.Fsi with
            | Some fsi ->
                // Only rewrite if FSAC returned 'fsi.exe' (For future-proofing)
                if path.basename fsi = "fsi.exe" then
                    // If there is an anyCpu variant in the same dir we do the rewrite
                    let anyCpuFile = path.join [| path.dirname fsi; "fsiAnyCpu.exe"|]
                    let! anyCpuExists = fileExists anyCpuFile
                    if anyCpuExists then
                        return Some anyCpuFile
                    else
                        return Some fsi
                else
                    return Some fsi
            | None ->
                return None
        }

        promise {
            match Environment.configFsiFilePath () with
            | Some path -> return Some path
            | None ->
                let! fsacPaths = fsacConfig ()
                let! fsiPath = getAnyCpuFsiPathFromCompilerLocation fsacPaths
                return fsiPath
        }

    let fsiSdk () =
        promise {
            return Environment.configFsiSdkFilePath ()
        }

    let fsc () =
        promise {
            match Environment.configFSCPath () with
            | Some path -> return Some path
            | None ->
                let! fsacPaths = fsacConfig ()
                return fsacPaths.Fsc
        }

    let msbuild () =
        promise {
            match Environment.configMSBuildPath with
            | Some path -> return Some path
            | None ->
                let! fsacPaths = fsacConfig ()
                return fsacPaths.MSBuild
        }

    let private createClient opts =
        let options =
            createObj [
                "run" ==> opts
                "debug" ==> opts
                ] |> unbox<ServerOptions>

        let fileDeletedWatcher = workspace.createFileSystemWatcher("*", true, true, false)

        let clientOpts =
            let opts = createEmpty<Client.LanguageClientOptions>
            let selector =
                createObj [
                    "scheme" ==> "*"
                    "language" ==> "qsp"
                ] |> unbox<Client.DocumentSelector>

            let initOpts =
                createObj [
                    "AutomaticWorkspaceInit" ==> false
                ]

            let synch = createEmpty<Client.SynchronizeOptions>
            synch.configurationSection <- Some !^"FSharp"
            synch.fileEvents <- Some( !^ ResizeArray([fileDeletedWatcher]))

            opts.documentSelector <- Some !^selector
            opts.synchronize <- Some synch
            opts.revealOutputChannelOn <- Some Client.RevealOutputChannelOn.Never


            opts.initializationOptions <- Some !^(Some initOpts)

            opts

        let cl = LanguageClient("FSharp", "QSP", options, clientOpts, false)
        client <- Some cl
        cl

    let getOptions () = promise {
        let spawnNetWin () =
            let fsautocompletePath =
                // if String.IsNullOrEmpty fsacNetPath then
                // VSCodeExtension.ionidePluginPath () + "/bin/YacsServer.exe"
                // "file:///E:/Project/YetAnotherSpellCheckerServer/YacsServer/bin/Debug/net461/YacsServer.exe"
                "E:/Project/YetAnotherSpellCheckerServer/YacsServer/bin/Debug/net461/YacsServer.exe"
                // else fsacNetPath
            printfn "FSAC (NET): '%s'" fsautocompletePath
            let args =
                [
                    // if backgroundSymbolCache then yield "--background-service-enabled"
                    // if verbose then yield  "--verbose"
                ] |> ResizeArray

            createObj [
                "command" ==> fsautocompletePath
                "args" ==> args
                "transport" ==> 0
            ]
        return spawnNetWin ()
    }
    let decorate =
        let decorationType =
            let opt = createEmpty<DecorationRenderOptions>
            // opt.
            // opt.backgroundColor <- Some (U2.Case1 "green")
            // opt.borderColor <- Some (U2.Case1 "2px solid white")
            opt.textDecoration <- Some "underline red"
            opt
        window.createTextEditorDecorationType decorationType
    let checkSpellAllDocument () =
        let editor = vscode.window.activeTextEditor

        try
            let text = editor.document.getText() // да-да, тупее выдумать не смог
            client
            |> Option.iter (fun client ->
                client.sendRequest("spellcheck/manual", text)
                |> Promise.map (fun (res : string) ->
                    // можно было написать `Promise.map (fun (res : Range ResizeArray) -> ...`,
                    // но из-за E:\Project\YetAnotherSpellCheckerServer\paket-files\fsharp\FsAutoComplete\src\LanguageServerProtocol\LanguageServerProtocol.fs:2170
                    // в итоге получаем объект: `{"startColumn":1,"startLine":1,"endColumn":5,"endLine":1}`
                    // хотя нужно: `{"StartColumn":1,"StartLine":1,"EndColumn":5,"EndLine":1}`
                    let res : Range ResizeArray = ofJson res
                    let xs =
                        res
                        |> Seq.map CodeRange.fromDTO
                        |> ResizeArray
                    editor.setDecorations(decorate, U2.Case1 xs)
                )
                |> ignore
            )
        with e ->
            e
            |> sprintf "%A\n%A" "editor.document.getText ()"
            |> vscode.window.showInformationMessage |> ignore


    let readyClient (ctx : ExtensionContext) (cl: LanguageClient) =
        cl.onReady ()
        |> Promise.onSuccess (fun _ ->
            cl.onNotification("spellcheck/decorate", fun res ->
                let editor = vscode.window.activeTextEditor
                let res : Range ResizeArray = ofJson res
                let xs =
                    res
                    |> Seq.map CodeRange.fromDTO
                    |> ResizeArray
                editor.setDecorations(decorate, U2.Case1 xs)
            )
            // cl.onNotification("textDocument/publishDiagnostics", fun (a) ->
            //     printfn "%A" a
            //     Diagnostic
            //     // failwith ""
            // )
            // cl.onNotification("fsharp/notifyWorkspace", (fun (a: Types.PlainNotification) ->
            //     match Notifications.notifyWorkspaceHandler with
            //     | None -> ()
            //     | Some cb ->
            //         let onMessage res =
            //             match res?Kind |> unbox with
            //             | "project" ->
            //                 res |> unbox<ProjectResult> |> deserializeProjectResult |> Choice1Of4 |> cb
            //             | "projectLoading" ->
            //                 res |> unbox<ProjectLoadingResult> |> Choice2Of4 |> cb
            //             | "error" ->
            //                 res?Data |> parseError |> Choice3Of4 |> cb
            //             | "workspaceLoad" ->
            //                 res?Data?Status |> unbox<string> |> Choice4Of4 |> cb
            //             | _ ->
            //                 ()
            //         let res = a.content |> ofJson<obj>
            //         onMessage res
            // ))

            // cl.onNotification("fsharp/fileParsed", (fun (a: Types.PlainNotification) ->
            //     let fn = a.content
            //     let te = window.visibleTextEditors |> Seq.find (fun n -> path.normalize(n.document.fileName).ToLower() = path.normalize(fn).ToLower())

            //     let ev = {Notifications.fileName = a.content; Notifications.version = te.document.version; Notifications.document = te.document }

            //     Notifications.onDocumentParsedEmitter.fire ev

            //     ()
            // ))

            let clearDecorations () =
                let editor = vscode.window.activeTextEditor
                // https://github.com/clarkio/vscode-twitch-highlighter/pull/6#issue-235128772
                editor.setDecorations(decorate, U2.Case1 (ResizeArray()))

            let disposable =
                vscode.commands.registerCommand("extension.helloWorld",
                    checkSpellAllDocument
                    |> unbox<Func<obj, obj>>
                )
            ctx.subscriptions.Add(disposable)

            vscode.commands.registerCommand("extension.clearDecoration",
                fun () -> clearDecorations()
                |> unbox<Func<obj, obj>>
            )
            |> ctx.subscriptions.Add

            vscode.window.showInformationMessage "client is ready" |> ignore
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
