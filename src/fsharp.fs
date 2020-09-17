[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Ionide.VSCode.FSharp
open Fable.Core
open Fable.Import
open Ionide.VSCode.FSharp

let activate (context : vscode.ExtensionContext) =
    LanguageService.start context
    |> Promise.catch (fun error ->
            vscode.window.showErrorMessage (sprintf "error %A" error) |> ignore
        ) // prevent unhandled rejected promises
    |> Promise.map (fun () ->
        HighlightingProvider.activate context
    )
let deactivate(disposables : vscode.Disposable[]) =
    LanguageService.stop ()
