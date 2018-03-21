/* ****************************************************************************
 * Copyright 2015 Steve Dower
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not
 * use this file except in compliance with the License. You may obtain a copy 
 * of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations
 * under the License.
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using IndentGuide.Utils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Brush = System.Windows.Media.Brush;
using Color = System.Drawing.Color;

namespace IndentGuide.Guides
{
    /// <summary>
    ///     Manages indent guides for a particular text view.
    /// </summary>
    public class IndentGuideView
    {
        private readonly Canvas Canvas;
        private readonly IDictionary<Color, Effect> GlowEffectCache;
        private readonly IDictionary<Color, Brush> GuideBrushCache;
        private readonly IAdornmentLayer Layer;
        private readonly Dictionary<LineSpan, Line> Lines = new Dictionary<LineSpan, Line>();
        private readonly IWpfTextView View;

        private DocumentAnalyzer Analysis;
        private bool GlobalVisible;
        private IndentTheme Theme;

        /// <summary>
        ///     Instantiates a new indent guide manager for a view.
        /// </summary>
        /// <param name="view">The text view to provide guides for.</param>
        /// <param name="service">The Indent Guide service.</param>
        public IndentGuideView(IWpfTextView view, IIndentGuide service)
        {
            GuideBrushCache = new Dictionary<Color, Brush>();
            GlowEffectCache = new Dictionary<Color, Effect>();

            View = view;
            View.Caret.PositionChanged += Caret_PositionChanged;
            View.LayoutChanged += View_LayoutChanged;
            View.Options.OptionChanged += View_OptionChanged;

            Layer = view.GetAdornmentLayer("IndentGuide");
            Canvas = new Canvas();
            Canvas.HorizontalAlignment = HorizontalAlignment.Stretch;
            Canvas.VerticalAlignment = VerticalAlignment.Stretch;
            Layer.AddAdornment(AdornmentPositioningBehavior.OwnerControlled, null, null, Canvas, CanvasRemoved);

            if (!service.Themes.TryGetValue(View.TextDataModel.ContentType.DisplayName, out Theme))
                Theme = service.DefaultTheme;
            Debug.Assert(Theme != null, "No themes loaded");
            if (Theme == null) Theme = new IndentTheme();
            service.ThemesChanged += Service_ThemesChanged;

            Analysis = new DocumentAnalyzer(
                View.TextSnapshot,
                Theme.Behavior,
                View.Options.GetOptionValue(DefaultOptions.IndentSizeOptionId),
                View.Options.GetOptionValue(DefaultOptions.TabSizeOptionId)
            );

            GlobalVisible = service.Visible;
            service.VisibleChanged += Service_VisibleChanged;

            Task t = AnalyzeAndUpdateAdornments();
        }

        private async Task AnalyzeAndUpdateAdornments(TextViewLayoutChangedEventArgs changes = null)
        {
            try
            {
                if (changes != null)
                    await Analysis.Update(changes);
                else
                    await Analysis.Reset();
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Errors.Log(ex);
            }

            UpdateAdornments();
        }

        /// <summary>
        ///     Raised when the canvas is removed.
        /// </summary>
        private void CanvasRemoved(object tag, UIElement element)
        {
            Layer.AddAdornment(AdornmentPositioningBehavior.OwnerControlled, null, null, Canvas, CanvasRemoved);
        }

        /// <summary>
        ///     Raised when the global visibility property is updated.
        /// </summary>
        private async void Service_VisibleChanged(object sender, EventArgs e)
        {
            GlobalVisible = ((IIndentGuide) sender).Visible;
            await AnalyzeAndUpdateAdornments();
        }

        /// <summary>
        ///     Raised when a view option changes.
        /// </summary>
        private async void View_OptionChanged(object sender, EditorOptionChangedEventArgs e)
        {
            if (e.OptionId == DefaultOptions.IndentSizeOptionId.Name)
            {
                Analysis = new DocumentAnalyzer(
                    View.TextSnapshot,
                    Theme.Behavior,
                    View.Options.GetOptionValue(DefaultOptions.IndentSizeOptionId),
                    View.Options.GetOptionValue(DefaultOptions.TabSizeOptionId)
                );
                GuideBrushCache.Clear();
                GlowEffectCache.Clear();

                await AnalyzeAndUpdateAdornments();
            }
        }

        /// <summary>
        ///     Raised when the theme is updated.
        /// </summary>
        private async void Service_ThemesChanged(object sender, EventArgs e)
        {
            IIndentGuide service = (IIndentGuide) sender;
            if (!service.Themes.TryGetValue(View.TextDataModel.ContentType.DisplayName, out Theme))
                Theme = service.DefaultTheme;

            Analysis = new DocumentAnalyzer(
                View.TextSnapshot,
                Theme.Behavior,
                View.Options.GetOptionValue(DefaultOptions.IndentSizeOptionId),
                View.Options.GetOptionValue(DefaultOptions.TabSizeOptionId)
            );
            GuideBrushCache.Clear();
            GlowEffectCache.Clear();

            await AnalyzeAndUpdateAdornments();
        }

        /// <summary>
        ///     Raised when the display changes.
        /// </summary>
        private async void View_LayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            await AnalyzeAndUpdateAdornments(e);
        }

        private IEnumerable<LineSpan> GetPageWidthLines()
        {
            return Theme.PageWidthMarkers
                .Select(i => new LineSpan(int.MinValue, int.MaxValue, i.Position, LineSpanType.PageWidthMarker));
        }

        /// <summary>
        ///     Recreates all adornments.
        /// </summary>
        private void UpdateAdornments()
        {
            if (View == null || Layer == null) return;
            try
            {
                if (View.TextViewLines == null) return;
            }
            catch (InvalidOperationException)
            {
                return;
            }

            if (Analysis == null) return;

            if (!View.VisualElement.Dispatcher.CheckAccess())
            {
                View.VisualElement.Dispatcher.InvokeAsync(UpdateAdornments);
                return;
            }

            try
            {
                UpdateAdornmentsWorker();
            }
            catch (Exception ex)
            {
                Errors.Log(ex);
            }
        }

        private void UpdateAdornmentsWorker()
        {
            //var analysisLines = Analysis.Lines;
            //if (Analysis.Snapshot != View.TextSnapshot) {
            //    var task = Analysis.Update();
            //    if (task != null) {
            //        UpdateAdornments(task);
            //    }
            //    return;
            //} else if (analysisLines == null) {
            //    UpdateAdornments(Analysis.Reset());
            //    return;
            //}

            if (!GlobalVisible)
            {
                Canvas.Visibility = Visibility.Collapsed;
                return;
            }

            Canvas.Visibility = Visibility.Visible;

            ITextSnapshot snapshot = View.TextSnapshot;
            ITextViewModel viewModel = View.TextViewModel;

            if (snapshot == null || viewModel == null) return;

            ITextViewLine firstVisibleLine = View.TextViewLines.FirstOrDefault(line => line.IsFirstTextViewLineForSnapshotLine);
            if (firstVisibleLine == null) return;
            ITextViewLine lastVisibleLine = View.TextViewLines.LastOrDefault(line => line.IsLastTextViewLineForSnapshotLine);
            if (lastVisibleLine == null) return;

            IEnumerable<LineSpan> analysisLines = Analysis.GetLines(
                firstVisibleLine.Start.GetContainingLine().LineNumber,
                lastVisibleLine.Start.GetContainingLine().LineNumber
            );

            if (!analysisLines.Any()) return;


#if PERFORMANCE
            object cookie = null;
            try {
                PerformanceLogger.Start(ref cookie);
                UpdateAdornments_Performance(
                    snapshot,
                    viewModel,
                    firstVisibleLine,
                    analysisLines
                );
            } catch (OperationCanceledException) {
                PerformanceLogger.Mark("Cancel");
                throw;
            } finally {
                PerformanceLogger.End(cookie);
            }
        }
        void UpdateAdornments_Performance(
            ITextSnapshot snapshot,
            ITextViewModel viewModel,
            ITextViewLine firstVisibleLine,
            IEnumerable<LineSpan> analysisLines
        ) {
#endif
            double spaceWidth = firstVisibleLine.VirtualSpaceWidth;
            if (spaceWidth <= 0.0) return;
            double horizontalOffset = firstVisibleLine.TextLeft;

            HashSet<LineSpan> unusedLines = new HashSet<LineSpan>(Lines.Keys);

            CaretHandlerBase caret = CaretHandlerBase.FromName(Theme.CaretHandler, View.Caret.Position.VirtualBufferPosition,
                Analysis.TabSize);

            object perfCookie = null;
            PerformanceLogger.Start(ref perfCookie);
#if DEBUG
            int initialCount = Lines.Count;
#endif
            foreach (LineSpan line in analysisLines.Concat(GetPageWidthLines()))
            {
                double top = View.ViewportTop;
                double bottom = View.ViewportBottom;
                double left = line.Indent * spaceWidth + horizontalOffset;

                Line adornment;
                unusedLines.Remove(line);

                if (line.Type == LineSpanType.PageWidthMarker)
                {
                    line.Highlight = Analysis.LongestLine > line.Indent;
                    if (!Lines.TryGetValue(line, out adornment)) Lines[line] = adornment = CreateGuide(Canvas);
                    UpdateGuide(line, adornment, left, top, bottom);
                    continue;
                }

                if (Lines.TryGetValue(line, out adornment)) adornment.Visibility = Visibility.Hidden;

                caret.AddLine(line, true);

                if (line.FirstLine >= 0 && line.LastLine < int.MaxValue)
                {
                    int firstLineNumber = line.FirstLine;
                    int lastLineNumber = line.LastLine;
                    ITextSnapshotLine firstLine, lastLine;
                    try
                    {
                        firstLine = snapshot.GetLineFromLineNumber(firstLineNumber);
                        lastLine = snapshot.GetLineFromLineNumber(lastLineNumber);
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError("In GetLineFromLineNumber:\n{0}", ex);
                        continue;
                    }

                    if (firstLine.Start > View.TextViewLines.LastVisibleLine.Start ||
                        lastLine.Start < View.TextViewLines.FirstVisibleLine.Start)
                        continue;

                    while (
                        !viewModel.IsPointInVisualBuffer(firstLine.Start, PositionAffinity.Successor) &&
                        ++firstLineNumber < lastLineNumber
                    )
                        try
                        {
                            firstLine = snapshot.GetLineFromLineNumber(firstLineNumber);
                        }
                        catch (Exception ex)
                        {
                            Trace.TraceError("In GetLineFromLineNumber:\n{0}", ex);
                            firstLine = null;
                            break;
                        }

                    while (
                        !viewModel.IsPointInVisualBuffer(lastLine.Start, PositionAffinity.Predecessor) &&
                        --lastLineNumber > firstLineNumber
                    )
                        try
                        {
                            lastLine = snapshot.GetLineFromLineNumber(lastLineNumber);
                        }
                        catch (Exception ex)
                        {
                            Trace.TraceError("In GetLineFromLineNumber:\n{0}", ex);
                            lastLine = null;
                            break;
                        }

                    if (firstLine == null || lastLine == null || firstLineNumber > lastLineNumber) continue;


                    IWpfTextViewLine firstView, lastView;
                    try
                    {
                        firstView = View.GetTextViewLineContainingBufferPosition(firstLine.Start);
                        lastView = View.GetTextViewLineContainingBufferPosition(lastLine.End);
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError("UpdateAdornments GetTextViewLineContainingBufferPosition failed\n{0}", ex);
                        continue;
                    }

                    string extentText;
                    if (!string.IsNullOrWhiteSpace(extentText = firstView.Extent.GetText()) &&
                        line.Indent > extentText.LeadingWhitespace(Analysis.TabSize)
                    )
                        continue;

                    if (firstView.VisibilityState != VisibilityState.Unattached) top = firstView.Top;
                    if (lastView.VisibilityState != VisibilityState.Unattached) bottom = lastView.Bottom;
                }

                if (!Lines.TryGetValue(line, out adornment)) Lines[line] = adornment = CreateGuide(Canvas);
                UpdateGuide(line, adornment, left, top, bottom);
            }

            PerformanceLogger.End(perfCookie);
#if DEBUG
            Debug.WriteLine("Added {0} guides", Lines.Count - initialCount);
            Debug.WriteLine("Removed {0} guides", unusedLines.Count);
            Debug.WriteLine("{0} guides active", Lines.Count - unusedLines.Count);
            Debug.WriteLine("");
#endif

            foreach (LineSpan line in unusedLines)
            {
                Line adornment;
                if (Lines.TryGetValue(line, out adornment))
                {
                    Canvas.Children.Remove(adornment);
                    Lines.Remove(line);
                }
            }

            foreach (LineSpan line in caret.GetModified())
            {
                Line adornment;
                if (Lines.TryGetValue(line, out adornment)) UpdateGuide(line, adornment);
            }
        }

        public Line CreateGuide(Canvas canvas)
        {
            if (canvas.Dispatcher.CheckAccess())
            {
                Line adornment = new Line();
                canvas.Children.Add(adornment);
                return adornment;
            }

            Debug.Fail("Do not call CreateGuide from a non-UI thread");
            return (Line) canvas.Dispatcher.Invoke((Func<Canvas, Line>) CreateGuide, canvas);
        }

        private void UpdateGuide(LineSpan lineSpan, Line adornment, double left, double top, double bottom)
        {
            if (bottom <= top)
            {
                adornment.Visibility = Visibility.Collapsed;
            }
            else
            {
                adornment.X1 = left + 0.5;
#if DEBUG
                adornment.X2 = left + 1.5;
#else
                adornment.X2 = left + 0.5;
#endif
                adornment.Y1 = top;
                adornment.Y2 = bottom;
                adornment.StrokeDashOffset = top;
                adornment.Visibility = Visibility.Visible;
                UpdateGuide(lineSpan, adornment);
            }
        }

        /// <summary>
        ///     Updates the line <paramref name="guide" /> with a new format.
        /// </summary>
        /// <param name="guide">The <see cref="Line" /> to update.</param>
        /// <param name="formatIndex">The new format index.</param>
        private void UpdateGuide(LineSpan lineSpan, Line adornment)
        {
            if (lineSpan == null || adornment == null) return;

            LineFormat format;
            if (lineSpan.Type == LineSpanType.PageWidthMarker)
            {
                if (!Theme.PageWidthMarkers.TryGetValue(lineSpan.Indent, out format)) format = Theme.DefaultLineFormat;
            }
            else if (!Theme.LineFormats.TryGetValue(lineSpan.FormatIndex, out format))
            {
                format = Theme.DefaultLineFormat;
            }

            if (!format.Visible)
            {
                adornment.Visibility = Visibility.Hidden;
                return;
            }

            bool highlight = lineSpan.Highlight || lineSpan.LinkedLines.Any(ls => ls.Highlight);

            LineStyle lineStyle = highlight ? format.HighlightStyle : format.LineStyle;
            Color lineColor = highlight && !lineStyle.HasFlag(LineStyle.Glow) ? format.HighlightColor : format.LineColor;

            Brush brush;
            if (!GuideBrushCache.TryGetValue(lineColor, out brush))
            {
                brush = new SolidColorBrush(lineColor.ToSWMC());
                if (brush.CanFreeze) brush.Freeze();
                GuideBrushCache[lineColor] = brush;
            }

            adornment.Stroke = brush;
            adornment.StrokeThickness = lineStyle.GetStrokeThickness();
            adornment.StrokeDashArray = lineStyle.GetStrokeDashArray();

            if (lineStyle.HasFlag(LineStyle.Dotted) || lineStyle.HasFlag(LineStyle.Dashed))
                adornment.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Unspecified);
            else
                adornment.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);

            if (lineStyle.HasFlag(LineStyle.Glow))
            {
                Effect effect;
                Color glowColor = highlight ? format.HighlightColor : format.LineColor;
                if (!GlowEffectCache.TryGetValue(glowColor, out effect))
                {
                    effect = new DropShadowEffect
                    {
                        Color = glowColor.ToSWMC(),
                        BlurRadius = LineStyle.Thick.GetStrokeThickness(),
                        Opacity = 1.0,
                        ShadowDepth = 0.0,
                        RenderingBias = RenderingBias.Performance
                    };
                    if (effect.CanFreeze) effect.Freeze();
                    GlowEffectCache[glowColor] = effect;
                }

                try
                {
                    adornment.Effect = effect;
                }
                catch (COMException)
                {
                    // No sensible way to deal with this exception, so we'll
                    // fall back on changing the color.
                    adornment.Effect = null;
                    if (!GuideBrushCache.TryGetValue(glowColor, out brush))
                    {
                        brush = new SolidColorBrush(glowColor.ToSWMC());
                        if (brush.CanFreeze) brush.Freeze();
                        GuideBrushCache[glowColor] = brush;
                    }

                    adornment.Stroke = brush;
                }
            }
            else
            {
                adornment.Effect = null;
            }
        }

        /// <summary>
        ///     Raised when the caret position changes.
        /// </summary>
        private void Caret_PositionChanged(object sender, CaretPositionChangedEventArgs e)
        {
            try
            {
                UpdateCaret(e.NewPosition.VirtualBufferPosition);
            }
            catch (Exception ex)
            {
                Errors.Log(ex);
            }
        }

        private void UpdateCaret(VirtualSnapshotPoint caretPosition)
        {
            IEnumerable<LineSpan> analysisLines = Analysis.GetAllLines();
            CaretHandlerBase caret = CaretHandlerBase.FromName(Theme.CaretHandler, caretPosition, Analysis.TabSize);

            foreach (LineSpan line in analysisLines)
            {
                int linePos = line.Indent;
                if (!Analysis.Behavior.VisibleUnaligned && linePos % Analysis.IndentSize != 0) continue;

                int formatIndex = line.Indent / Analysis.IndentSize;

                if (line.Indent % Analysis.IndentSize != 0) formatIndex = LineFormat.UnalignedFormatIndex;

                caret.AddLine(line, false);
            }

            foreach (LineSpan line in caret.GetModified())
            {
                Line adornment;
                if (Lines.TryGetValue(line, out adornment)) UpdateGuide(line, adornment);
            }
        }
    }
}