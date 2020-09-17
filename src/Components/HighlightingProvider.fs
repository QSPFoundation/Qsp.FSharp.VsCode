namespace Ionide.VSCode.FSharp

open System
open Fable.Core.JsInterop
open Fable.Import
open Fable.Import.vscode

open DTO
open Ionide.VSCode.Helpers

// https://github.com/microsoft/vscode-extension-samples/blob/master/semantic-tokens-sample/src/extension.ts
module HighlightingProvider =
    let tokenTypes =
        [| "comment"; "string"; "keyword"; "number"; "regexp"; "operator"; "namespace";
           "type"; "struct"; "class"; "interface"; "enum"; "enumMember"; "typeParameter"; "function";
           "member"; "macro"; "variable"; "parameter"; "property"; "label";
           "storage"
           "procedure"
           "keywordControl"
           "operatorArithmetic"
           "operatorAssignment"
           "operatorComparison"
           "operatorRelational"
           "punctuationTerminatorStatement"
           "punctuationSeparatorColon"
           "interpolationBegin"
           "interpolationEnd"
           "constantNumericInteger"
           "metaBraceSquare"
       |]
    // TODO: а еще есть [tokenModifiersLegend](https://github.com/microsoft/vscode-extension-samples/blob/f2a57780639bce99099d5128db2d3be83012d0c0/semantic-tokens-sample/src/extension.ts#L14)
    let legend = SemanticTokensLegend(tokenTypes)

    let provider =
        { new DocumentSemanticTokensProvider
          with
            member __.provideDocumentSemanticTokens(textDocument, ct) =
                promise {
                    let builder = SemanticTokensBuilder(legend)
                    let! res = LanguageService.getHighlighting (textDocument.fileName)
                    match res with
                    | Ok res ->
                        res.Data.Highlights
                        |> Array.sortBy(fun n -> n.Range.StartLine * 1000000 + n.Range.StartColumn)
                        |> Array.iter (fun n ->
                            builder.push(CodeRange.fromDTO n.Range, n.TokenType)
                        )
                        return builder.build()
                    | Error x ->
                        vscode.window.showErrorMessage x |> ignore
                        return null
                } |> unbox
        }

    let activate (context : ExtensionContext) =
        let df = createEmpty<DocumentFilter>
        df.language <- Some "qsp"

        languages.registerDocumentSemanticTokensProvider(!!df, provider, legend )  |> context.subscriptions.Add

        ()