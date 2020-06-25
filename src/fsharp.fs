[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Ionide.VSCode.FSharp

open System
open Fable.Core
open Fable.Core.JsInterop
open Fable.Import
open Ionide.VSCode.Helpers
open Ionide.VSCode.FSharp
open Fable.Import.Node.ChildProcess
open Fable.Import.vscode

let activate (context : vscode.ExtensionContext) =
    LanguageService.start context
    |> Promise.catch (fun error -> promise { () }) // prevent unhandled rejected promises

let deactivate(disposables : vscode.Disposable[]) =
    LanguageService.stop ()
