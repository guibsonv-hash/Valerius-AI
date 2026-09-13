using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using D = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;
using P = DocumentFormat.OpenXml.Presentation;
using S = DocumentFormat.OpenXml.Spreadsheet;
using W = DocumentFormat.OpenXml.Wordprocessing;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Fonts;
using ValeriusAI.Core;

namespace ValeriusAI.Infrastructure;

public sealed class ArtifactService(IWorkspaceRepository repository, string outputDirectory)
{
    public static readonly string[] Types = ["docx", "xlsx", "pptx", "pdf", "md", "txt", "csv", "json"];

    public async Task<Artifact> CreateAsync(string taskId, string name, string type, string content, CancellationToken ct = default)
    {
        type = type.Trim('.').ToLowerInvariant();
        if (!Types.Contains(type)) throw new NotSupportedException("Tipo de arquivo não suportado.");
        Directory.CreateDirectory(outputDirectory);
        var safeName = string.Join("-", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "resultado";
        var path = Unique(Path.Combine(outputDirectory, safeName + "." + type));
        await Task.Run(() => Write(path, type, content), ct);
        Validate(path, type);
        var info = new FileInfo(path);
        var artifact = new Artifact(Guid.NewGuid().ToString(), taskId, info.Name, info.FullName, type.ToUpperInvariant(), info.Length, DateTimeOffset.UtcNow);
        await repository.SaveArtifactAsync(artifact);
        return artifact;
    }

    public async Task DeleteAsync(Artifact artifact, CancellationToken ct = default)
    {
        var root=Path.GetFullPath(outputDirectory).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        var path=Path.GetFullPath(artifact.Path);
        if(!path.StartsWith(root,StringComparison.Ordinal))throw new InvalidOperationException("O arquivo não pertence à Biblioteca local.");
        if(File.Exists(path))await Task.Run(()=>File.Delete(path),ct);
        await repository.DeleteArtifactAsync(artifact.Id);
    }

    private static void Write(string path, string type, string content)
    {
        if (type is "md" or "txt" or "csv" or "json") { File.WriteAllText(path, content); return; }
        if (type == "docx") { WriteDocx(path, content); return; }
        if (type == "xlsx") { WriteXlsx(path, content); return; }
        if (type == "pptx") { WritePptx(path, content); return; }
        WritePdf(path, content);
    }

    private static void WriteDocx(string path, string content)
    {
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var main = doc.AddMainDocumentPart();
        var body = new W.Body(); main.Document = new W.Document(body);
        var values = Lines(content).ToList();
        for (var index = 0; index < values.Count; index++)
        {
            var line = values[index];
            if (line.Contains(',') && values.Skip(index).TakeWhile(x => x.Contains(',')).Count() >= 2)
            {
                var tableLines = values.Skip(index).TakeWhile(x => x.Contains(',')).ToList();
                var table = new W.Table(new W.TableProperties(new W.TableBorders(
                    new W.TopBorder { Val = W.BorderValues.Single, Size = 6 }, new W.LeftBorder { Val = W.BorderValues.Single, Size = 6 },
                    new W.BottomBorder { Val = W.BorderValues.Single, Size = 6 }, new W.RightBorder { Val = W.BorderValues.Single, Size = 6 },
                    new W.InsideHorizontalBorder { Val = W.BorderValues.Single, Size = 4 }, new W.InsideVerticalBorder { Val = W.BorderValues.Single, Size = 4 })));
                foreach (var tableLine in tableLines) table.Append(new W.TableRow(tableLine.Split(',').Select(value => new W.TableCell(new W.Paragraph(new W.Run(new W.Text(value.Trim())))))));
                body.Append(table); index += tableLines.Count - 1; continue;
            }
            var paragraph = new W.Paragraph(new W.Run(new W.Text(line) { Space = SpaceProcessingModeValues.Preserve }));
            if (index == 0) paragraph.ParagraphProperties = new W.ParagraphProperties(new W.ParagraphStyleId { Val = "Title" });
            else if (line.Length < 70 && !line.EndsWith('.')) paragraph.ParagraphProperties = new W.ParagraphProperties(new W.ParagraphStyleId { Val = "Heading1" });
            body.Append(paragraph);
        }
        body.Append(new W.SectionProperties(new W.PageMargin { Top = 1134, Right = 1134, Bottom = 1134, Left = 1134 }));
        main.Document.Save();
    }

    private static void WriteXlsx(string path, string content)
    {
        using var doc = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook);
        var wb = doc.AddWorkbookPart(); wb.Workbook = new S.Workbook();
        var ws = wb.AddNewPart<WorksheetPart>(); var data = new S.SheetData();
        var rows = Lines(content).Select(line => line.Split([',', '\t']).Select(value => value.Trim()).ToArray()).ToList();
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = new S.Row();
            for (var columnIndex = 0; columnIndex < rows[rowIndex].Length; columnIndex++)
            {
                var value = rows[rowIndex][columnIndex]; var cell = new S.Cell { CellReference = Column(columnIndex + 1) + (rowIndex + 1) };
                if (rowIndex > 0 && double.TryParse(value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var number)) { cell.DataType = S.CellValues.Number; cell.CellValue = new S.CellValue(number); }
                else { cell.DataType = S.CellValues.InlineString; cell.InlineString = new S.InlineString(new S.Text(value)); }
                row.Append(cell);
            }
            data.Append(row);
        }
        var maxColumns = Math.Max(1, rows.Max(x => x.Length)); var totalRow = rows.Count + 1;
        var total = new S.Row { RowIndex = (uint)totalRow }; total.Append(new S.Cell { CellReference = "A" + totalRow, DataType = S.CellValues.InlineString, InlineString = new S.InlineString(new S.Text("Total")) });
        for (var column = 2; column <= maxColumns; column++) total.Append(new S.Cell { CellReference = Column(column) + totalRow, CellFormula = new S.CellFormula($"SUM({Column(column)}2:{Column(column)}{rows.Count})") }); data.Append(total);
        var columns = new S.Columns(); for (uint i = 1; i <= maxColumns; i++) columns.Append(new S.Column { Min = i, Max = i, Width = 20, CustomWidth = true });
        var panes = new S.SheetViews(new S.SheetView(new S.Pane { VerticalSplit = 1, TopLeftCell = "A2", ActivePane = S.PaneValues.BottomLeft, State = S.PaneStateValues.Frozen }) { WorkbookViewId = 0U });
        ws.Worksheet = new S.Worksheet(panes, columns, data, new S.AutoFilter { Reference = $"A1:{Column(maxColumns)}{rows.Count}" });
        var sheets = wb.Workbook.AppendChild(new S.Sheets()); sheets.Append(new S.Sheet { Id = wb.GetIdOfPart(ws), SheetId = 1, Name = "Dados" });
        if (maxColumns >= 2 && rows.Count >= 3) AddChart(ws, rows.Count, maxColumns);
        wb.Workbook.Save();
    }

    private static void AddChart(WorksheetPart worksheet, int rowCount, int columnCount)
    {
        var drawings = worksheet.AddNewPart<DrawingsPart>(); worksheet.Worksheet!.Append(new S.Drawing { Id = worksheet.GetIdOfPart(drawings) });
        drawings.WorksheetDrawing = new Xdr.WorksheetDrawing(); var chartPart = drawings.AddNewPart<ChartPart>();
        var chartSpace = new C.ChartSpace(); chartSpace.Append(new C.EditingLanguage { Val = "pt-BR" }); var chart = chartSpace.AppendChild(new C.Chart());
        chart.Append(new C.AutoTitleDeleted { Val = true }); var plot = chart.AppendChild(new C.PlotArea()); plot.Append(new C.Layout()); var bar = plot.AppendChild(new C.BarChart(new C.BarDirection { Val = C.BarDirectionValues.Column }, new C.BarGrouping { Val = C.BarGroupingValues.Clustered }, new C.VaryColors { Val = false }));
        uint index = 0; for (var column = 2; column <= Math.Min(columnCount, 4); column++, index++)
        {
            var series = new C.BarChartSeries(new C.Index { Val = index }, new C.Order { Val = index }, new C.SeriesText(new C.StringReference(new C.Formula($"Dados!${Column(column)}$1"))),
                new C.CategoryAxisData(new C.StringReference(new C.Formula($"Dados!$A$2:$A${rowCount}"))), new C.Values(new C.NumberReference(new C.Formula($"Dados!${Column(column)}$2:${Column(column)}${rowCount}")))); bar.Append(series);
        }
        const uint catAxis = 48650112U, valueAxis = 48672768U; bar.Append(new C.AxisId { Val = catAxis }, new C.AxisId { Val = valueAxis });
        plot.Append(new C.CategoryAxis(new C.AxisId { Val = catAxis }, new C.Scaling(new C.Orientation { Val = C.OrientationValues.MinMax }), new C.Delete { Val = false }, new C.AxisPosition { Val = C.AxisPositionValues.Bottom }, new C.TickLabelPosition { Val = C.TickLabelPositionValues.NextTo }, new C.CrossingAxis { Val = valueAxis }, new C.Crosses { Val = C.CrossesValues.AutoZero }, new C.AutoLabeled { Val = true }, new C.LabelAlignment { Val = C.LabelAlignmentValues.Center }, new C.LabelOffset { Val = 100 }));
        plot.Append(new C.ValueAxis(new C.AxisId { Val = valueAxis }, new C.Scaling(new C.Orientation { Val = C.OrientationValues.MinMax }), new C.Delete { Val = false }, new C.AxisPosition { Val = C.AxisPositionValues.Left }, new C.MajorGridlines(), new C.NumberingFormat { FormatCode = "General", SourceLinked = true }, new C.TickLabelPosition { Val = C.TickLabelPositionValues.NextTo }, new C.CrossingAxis { Val = catAxis }, new C.Crosses { Val = C.CrossesValues.AutoZero }, new C.CrossBetween { Val = C.CrossBetweenValues.Between }));
        chart.Append(new C.PlotVisibleOnly { Val = true }, new C.DisplayBlanksAs { Val = C.DisplayBlanksAsValues.Gap }); chartPart.ChartSpace = chartSpace; chartPart.ChartSpace.Save();
        var frame = new Xdr.GraphicFrame(new Xdr.NonVisualGraphicFrameProperties(new Xdr.NonVisualDrawingProperties { Id = 2U, Name = "Gráfico" }, new Xdr.NonVisualGraphicFrameDrawingProperties()), new Xdr.Transform(), new D.Graphic(new D.GraphicData(new C.ChartReference { Id = drawings.GetIdOfPart(chartPart) }) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/chart" }));
        drawings.WorksheetDrawing.Append(new Xdr.TwoCellAnchor(new Xdr.FromMarker(new Xdr.ColumnId("0"), new Xdr.ColumnOffset("0"), new Xdr.RowId((rowCount + 2).ToString()), new Xdr.RowOffset("0")), new Xdr.ToMarker(new Xdr.ColumnId("8"), new Xdr.ColumnOffset("0"), new Xdr.RowId((rowCount + 20).ToString()), new Xdr.RowOffset("0")), frame, new Xdr.ClientData())); drawings.WorksheetDrawing.Save();
    }

    private static void WritePptx(string path, string content)
    {
        using var doc = PresentationDocument.Create(path, PresentationDocumentType.Presentation);
        var part = doc.AddPresentationPart(); part.Presentation = new P.Presentation();
        var source = Lines(content).ToList(); var chunks = Enumerable.Range(0,5).Select(i=>source.Where((_,index)=>index%5==i).DefaultIfEmpty(i==0?"Apresentação criada pelo Valerius AI":"Conteúdo complementar").ToList()).ToList();
        var size = new P.SlideSize { Cx = 12192000, Cy = 6858000, Type = P.SlideSizeValues.Screen16x9 };
        var ids = new P.SlideIdList(); uint slideId=256;
        foreach(var chunk in chunks){var slidePart=part.AddNewPart<SlidePart>();slidePart.Slide=new P.Slide(new P.CommonSlideData(new P.ShapeTree(new P.NonVisualGroupShapeProperties(new P.NonVisualDrawingProperties{Id=1U,Name=""},new P.NonVisualGroupShapeDrawingProperties(),new P.ApplicationNonVisualDrawingProperties()),new P.GroupShapeProperties(new D.TransformGroup()),TextShape(2U,"Conteúdo",string.Join('\n',chunk)))));ids.Append(new P.SlideId{Id=slideId++,RelationshipId=part.GetIdOfPart(slidePart)});}
        part.Presentation.Append(ids, size, new P.NotesSize { Cx = 6858000, Cy = 9144000 }); part.Presentation.Save();
    }

    private static P.Shape TextShape(uint id, string name, string content)
    {
        var props = new P.NonVisualShapeProperties(new P.NonVisualDrawingProperties { Id = id, Name = name }, new P.NonVisualShapeDrawingProperties(new D.ShapeLocks { NoGrouping = true }), new P.ApplicationNonVisualDrawingProperties());
        var shapeProps = new P.ShapeProperties(new D.Transform2D(new D.Offset { X = 600000, Y = 500000 }, new D.Extents { Cx = 10900000, Cy = 5800000 }), new D.PresetGeometry { Preset = D.ShapeTypeValues.Rectangle });
        var body = new P.TextBody(new D.BodyProperties { Wrap = D.TextWrappingValues.Square }, new D.ListStyle());
        foreach (var line in Lines(content).Take(16)) body.Append(new D.Paragraph(new D.Run(new D.RunProperties { Language = "pt-BR", FontSize = 2200 }, new D.Text(line)), new D.EndParagraphRunProperties { Language = "pt-BR" }));
        return new P.Shape(props, shapeProps, body);
    }

    private static void WritePdf(string path, string content)
    {
        PdfFont.Ensure();
        using var document = new PdfDocument();
        var font = new XFont("Valerius Sans", 11);
        var lines = Wrap(content, 88).ToList();
        for (var start = 0; start < lines.Count; start += 42)
        {
            var page = document.AddPage(); page.Size = PdfSharp.PageSize.A4;
            using var graphics = XGraphics.FromPdfPage(page); var y = 54d;
            foreach (var line in lines.Skip(start).Take(42)) { graphics.DrawString(line, font, XBrushes.Black, new XPoint(54, y)); y += 17; }
        }
        document.Save(path);
    }

    private sealed class PdfFont : IFontResolver
    {
        private static int configured;
        private const string Face = "ValeriusSansRegular";
        private readonly byte[] bytes;
        private PdfFont()
        {
            var candidates = new[] { "/System/Library/Fonts/Supplemental/Arial.ttf", "/Library/Fonts/Arial.ttf", "C:/Windows/Fonts/arial.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf" };
            var path = candidates.FirstOrDefault(File.Exists) ?? throw new FileNotFoundException("Nenhuma fonte compatível foi encontrada para criar o PDF.");
            bytes = File.ReadAllBytes(path);
        }
        public static void Ensure()
        {
            if (Interlocked.Exchange(ref configured, 1) == 0) GlobalFontSettings.FontResolver = new PdfFont();
        }
        public byte[]? GetFont(string faceName) => faceName == Face ? bytes : null;
        public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic) => new(Face);
    }

    private static void Validate(string path, string type)
    {
        if (!File.Exists(path) || new FileInfo(path).Length == 0) throw new InvalidDataException("O arquivo gerado ficou vazio.");
        if (type == "docx") { using var _ = WordprocessingDocument.Open(path, false); }
        else if (type == "xlsx") { using var _ = SpreadsheetDocument.Open(path, false); }
        else if (type == "pptx") { using var _ = PresentationDocument.Open(path, false); }
        else if (type == "pdf") { using var _ = PdfReader.Open(path, PdfDocumentOpenMode.Import); }
    }

    private static string Unique(string path)
    { if (!File.Exists(path)) return path; return Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + Path.GetExtension(path)); }
    private static IEnumerable<string> Lines(string text) => text.Replace("\r", "").Split('\n').Where(x => !string.IsNullOrWhiteSpace(x)).DefaultIfEmpty("Resultado criado pelo Valerius AI");
    private static string Column(int number){var result="";while(number>0){number--;result=(char)('A'+number%26)+result;number/=26;}return result;}
    private static IEnumerable<string> Wrap(string text, int width)
    {
        foreach (var paragraph in Lines(text))
        {
            var remaining = paragraph.Trim();
            while (remaining.Length > width) { var i = remaining.LastIndexOf(' ', width); if (i < 1) i = width; yield return remaining[..i]; remaining = remaining[i..].TrimStart(); }
            yield return remaining;
        }
    }
}
