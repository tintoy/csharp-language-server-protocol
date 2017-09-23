using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using JsonRpc;
using Lsp;
using Lsp.Capabilities.Client;
using Lsp.Capabilities.Server;
using Lsp.Models;
using Lsp.Protocol;

using SaveOptions = Lsp.Capabilities.Server.SaveOptions;

namespace SampleServer
{
    class Program
    {
        static void Main(string[] args)
        {
            MainAsync(args).Wait();
        }

        static async Task MainAsync(string[] args)
        {
            //while (!System.Diagnostics.Debugger.IsAttached)
            //{
            //    await Task.Delay(100);
            //}

            var server = new LanguageServer(Console.OpenStandardInput(), Console.OpenStandardOutput());

            server.AddHandler(new TextDocumentHandler(server));

            await server.Initialize();
            await server.WasShutDown;
        }
    }

    class TextDocumentHandler : ITextDocumentSyncHandler, IHoverHandler
    {
        private readonly ILanguageServer _router;

        private readonly DocumentSelector _documentSelector = new DocumentSelector(
            new DocumentFilter() {
                Pattern = "**/*.csproj",
                Language = "xml"
            }
        );

        private SynchronizationCapability _synchronizationCapability;
        private HoverCapability _hoverCapability;

        public TextDocumentHandler(ILanguageServer router)
        {
            _router = router;
        }

        public TextDocumentSyncOptions Options { get; } = new TextDocumentSyncOptions() {
            WillSaveWaitUntil = false,
            WillSave = true,
            Change = TextDocumentSyncKind.Full,
            Save = new SaveOptions() {
                IncludeText = true
            },
            OpenClose = true
        };

        public Task Handle(DidChangeTextDocumentParams notification)
        {
            _router.LogMessage(new LogMessageParams() {
                Type = MessageType.Log,
                Message = "Hello World!!!!"
            });
            return Task.CompletedTask;
        }

        TextDocumentChangeRegistrationOptions IRegistration<TextDocumentChangeRegistrationOptions>.GetRegistrationOptions()
        {
            return new TextDocumentChangeRegistrationOptions() {
                DocumentSelector = _documentSelector,
                SyncKind = Options.Change
            };
        }

        public void SetCapability(SynchronizationCapability capability)
        {
            _synchronizationCapability = capability;
        }

        public async Task Handle(DidOpenTextDocumentParams notification)
        {
            _router.LogMessage(new LogMessageParams() {
                Type = MessageType.Log,
                Message = "Hello World!!!!"
            });
        }

        TextDocumentRegistrationOptions IRegistration<TextDocumentRegistrationOptions>.GetRegistrationOptions()
        {
            return new TextDocumentRegistrationOptions() {
                DocumentSelector = _documentSelector,
            };
        }

        public Task Handle(DidCloseTextDocumentParams notification)
        {
            return Task.CompletedTask;
        }

        public Task Handle(DidSaveTextDocumentParams notification)
        {
            return Task.CompletedTask;
        }

        TextDocumentSaveRegistrationOptions IRegistration<TextDocumentSaveRegistrationOptions>.GetRegistrationOptions()
        {
            return new TextDocumentSaveRegistrationOptions() {
                DocumentSelector = _documentSelector,
                IncludeText = Options.Save.IncludeText
            };
        }
        public TextDocumentAttributes GetTextDocumentAttributes(Uri uri)
        {
            return new TextDocumentAttributes(uri, "csharp");
        }

        public async Task<Hover> Handle(TextDocumentPositionParams request, CancellationToken token)
        {
            string documentPath = request.TextDocument.Uri.LocalPath.Substring(1).Replace('/', '\\');
            Debug.WriteLine($"Parse '{documentPath}'.");

            XDocument xml = XDocument.Load(documentPath,
                LoadOptions.PreserveWhitespace | LoadOptions.SetBaseUri | LoadOptions.SetLineInfo
            );

            StringBuilder contentBuilder = new StringBuilder("Ranges:\n");

            Range targetRange = null;
            try
            {
                var packageReferenceElements = xml.Descendants("PackageReference");
                foreach (XElement packageReferenceElement in packageReferenceElements)
                {
                    string message;

                    targetRange = GetRange(packageReferenceElement);
                    if (!RangeContainsPosition(targetRange, request.Position))
                    {
                        targetRange = null;

                        continue;
                    }

                    XAttribute includeAttribute = packageReferenceElement.Attribute("Include");
                    if (includeAttribute != null)
                    {
                        Range attributeRange = GetRange(includeAttribute);
                        if (RangeContainsPosition(attributeRange, request.Position))
                            targetRange = attributeRange;

                        message = $"PackageReference.Include attribute from ({attributeRange.Start.Line}, {attributeRange.Start.Character}) to ({attributeRange.End.Line}, {attributeRange.End.Character}).";
                        Debug.WriteLine(message);
                        contentBuilder.AppendLine(message);
                    }

                    XAttribute versionAttribute = packageReferenceElement.Attribute("Version");
                    if (versionAttribute != null)
                    {
                        Range attributeRange = GetRange(versionAttribute);
                        if (RangeContainsPosition(attributeRange, request.Position))
                            targetRange = attributeRange;

                        message = $"PackageReference.Version attribute from ({attributeRange.Start.Line}, {attributeRange.Start.Character}) to ({attributeRange.End.Line}, {attributeRange.End.Character}).";
                        Debug.WriteLine(message);
                        contentBuilder.AppendLine(message);
                    }

                    message = $"PackageReference element from ({targetRange.Start.Line}, {targetRange.Start.Character}) to ({targetRange.End.Line}, {targetRange.End.Character}).";
                    Debug.WriteLine(message);
                    contentBuilder.AppendLine(message);
                }
            }
            catch (Exception eUnexpected)
            {
                contentBuilder.Clear();
                contentBuilder.AppendLine(eUnexpected.ToString());
            }

            return new Hover
            {
                Contents = $"Hover for ({request.Position.Line}, {request.Position.Character}):\n{contentBuilder}",
                Range = targetRange ?? new Range(
                    start: request.Position,
                    end: new Position(request.Position.Line, request.Position.Character + 5)
                )
            };
        }

        public void SetCapability(HoverCapability hoverCapability)
        {
            _hoverCapability = hoverCapability;
        }

        static TextDocumentHandler()
        {
            Dictionary<string, Type> types =
                typeof(XElement).GetTypeInfo().Assembly.GetTypes().ToDictionary(
                    type => type.FullName
                );

            Debug.WriteLine(types.Count);
            LineInfoAnnotation = types["System.Xml.Linq.LineInfoAnnotation"];
            LineInfoEndElementAnnotation = types["System.Xml.Linq.LineInfoEndElementAnnotation"];

            LineInfoLineNumber = LineInfoAnnotation.GetField("lineNumber", BindingFlags.Instance | BindingFlags.NonPublic);
            LineInfoLinePosition = LineInfoAnnotation.GetField("linePosition", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        static readonly Type LineInfoAnnotation;
        static readonly Type LineInfoEndElementAnnotation;
        static readonly FieldInfo LineInfoLineNumber;
        static readonly FieldInfo LineInfoLinePosition;

        static Range GetRange(XNode node)
        {
            Position start;
            Position end;

            object startLineInfo = node.Annotations<object>().FirstOrDefault(
                annotation => annotation.GetType() == LineInfoAnnotation
            );
            if (startLineInfo == null)
                throw new Exception("LineInfoAnnotation not found.");

            start = GetLineInfoAsPosition(startLineInfo);
            if (start.Character > 0)
                start.Character--;

            object endLineInfo = node.Annotations<object>().FirstOrDefault(
                annotation => annotation.GetType() == LineInfoEndElementAnnotation
            );
            if (endLineInfo == null)
            {
                // Try for start of next node.
                endLineInfo = node.NextNode.Annotations<object>().FirstOrDefault(
                    annotation => annotation.GetType() == LineInfoAnnotation
                );
                if (endLineInfo == null)
                    throw new Exception("LineInfoAnnotation not found for end.");

                end = GetLineInfoAsPosition(endLineInfo);
            }
            else
                end = GetLineInfoAsPosition(endLineInfo);
            
            return new Range(start, end);
        }

        static Range GetRange(XAttribute attribute)
        {
            Position start;
            Position end;

            object startLineInfo = attribute.Annotations<object>().FirstOrDefault(
                annotation => annotation.GetType() == LineInfoAnnotation
            );
            if (startLineInfo == null)
                throw new Exception("LineInfoAnnotation not found.");

            start = GetLineInfoAsPosition(startLineInfo);

            //object endLineInfo = attribute.Annotations<object>().FirstOrDefault(
            //    annotation => annotation.GetType() == LineInfoEndElementAnnotation
            //);
            //if (endLineInfo == null && attribute.NextAttribute != null)
            //{
            //    endLineInfo = attribute.NextAttribute.Annotations<object>().FirstOrDefault(
            //        annotation => annotation.GetType() == LineInfoAnnotation
            //    );
            //}
            //if (endLineInfo == null && attribute.Parent.NextNode != null)
            //{
            //    endLineInfo = attribute.Parent.NextNode.Annotations<object>().FirstOrDefault(
            //        annotation => annotation.GetType() == LineInfoAnnotation
            //    );
            //}

            //if (endLineInfo == null)
            //    throw new Exception("LineInfoAnnotation not found for end.");

            //end = GetLineInfoAsPosition(endLineInfo);

            // AF: Hacky! TODO: Make a console app to quickly figure out how we can see the whitespace between attributes.
            // AF: Appears like this won't be possible (whitespace between attributes is not captured so we may need to infer its distance from next attribute / node's position).
            end = GetLineInfoAsPosition(startLineInfo);
            end.Character += attribute.Name.LocalName.Length + "=\"".Length + attribute.Value.Length + "\"".Length;

            return new Range(start, end);
        }

        static Position GetLineInfoAsPosition(object lineInfo)
        {
            return new Position(
                (int)LineInfoLineNumber.GetValue(lineInfo) - 1,
                (int)LineInfoLinePosition.GetValue(lineInfo) - 1
            );
        }

        static bool RangeContainsPosition(Range range, Position position)
        {
            if (position.Line < range.Start.Line)
                return false;

            if (position.Line > range.End.Line)
                return false;

            if (position.Character < range.Start.Character)
                return false;

            if (position.Character > range.End.Character)
                return false;

            return true;
        }
    }
}