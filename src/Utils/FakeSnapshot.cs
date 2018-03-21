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
using System.IO;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace IndentGuide.Utils
{
    internal class FakeLine : ITextSnapshotLine
    {
        internal readonly int _Line, _Start;
        internal readonly string _Text;

        public FakeLine(ITextSnapshot snapshot, string text, int line, int start)
        {
            Snapshot = snapshot;
            if (text.EndsWith("\r"))
                _Text = text.Substring(0, text.Length - 1);
            else
                _Text = text;
            _Line = line;
            _Start = start;
        }

        public SnapshotPoint Start => new SnapshotPoint(Snapshot, _Start);
        public SnapshotPoint End => new SnapshotPoint(Snapshot, _Start + Length);
        public SnapshotPoint EndIncludingLineBreak => new SnapshotPoint(Snapshot, _Start + LengthIncludingLineBreak);
        public SnapshotSpan Extent => new SnapshotSpan(Start, End);
        public SnapshotSpan ExtentIncludingLineBreak => new SnapshotSpan(Start, EndIncludingLineBreak);

        public string GetLineBreakText()
        {
            return "\n";
        }

        public string GetText()
        {
            return _Text;
        }

        public string GetTextIncludingLineBreak()
        {
            return GetText() + GetLineBreakText();
        }

        public int Length => _Text.Length;
        public int LengthIncludingLineBreak => Length + LineBreakLength;
        public int LineBreakLength => GetLineBreakText().Length;
        public int LineNumber => _Line;
        public ITextSnapshot Snapshot { get; set; }
    }

    internal class FakeContentType : IContentType
    {
        public FakeContentType(string name)
        {
            TypeName = name;
        }

        public IEnumerable<IContentType> BaseTypes => throw new NotImplementedException();
        public string DisplayName => TypeName;

        public bool IsOfType(string type)
        {
            return string.Equals(type, TypeName);
        }

        public string TypeName { get; set; }
    }


    internal class FakeTextBuffer : ITextBuffer
    {
        public ITextSnapshot CurrentSnapshot { get; set; }

        public void ChangeContentType(IContentType newContentType, object editTag)
        {
            throw new NotImplementedException();
        }

        public event EventHandler<TextContentChangedEventArgs> Changed
        {
            add { }
            remove { }
        }

        public event EventHandler<TextContentChangedEventArgs> ChangedHighPriority
        {
            add { }
            remove { }
        }

        public event EventHandler<TextContentChangedEventArgs> ChangedLowPriority
        {
            add { }
            remove { }
        }

        public event EventHandler<TextContentChangingEventArgs> Changing
        {
            add { }
            remove { }
        }

        public bool CheckEditAccess()
        {
            throw new NotImplementedException();
        }

        public IContentType ContentType => new FakeContentType("CSharp");

        public event EventHandler<ContentTypeChangedEventArgs> ContentTypeChanged
        {
            add { }
            remove { }
        }

        public ITextEdit CreateEdit()
        {
            throw new NotImplementedException();
        }

        public ITextEdit CreateEdit(EditOptions options, int? reiteratedVersionNumber, object editTag)
        {
            throw new NotImplementedException();
        }

        public IReadOnlyRegionEdit CreateReadOnlyRegionEdit()
        {
            throw new NotImplementedException();
        }

        public ITextSnapshot Delete(Span deleteSpan)
        {
            throw new NotImplementedException();
        }

        public bool EditInProgress => throw new NotImplementedException();

        public NormalizedSpanCollection GetReadOnlyExtents(Span span)
        {
            throw new NotImplementedException();
        }

        public ITextSnapshot Insert(int position, string text)
        {
            throw new NotImplementedException();
        }

        public bool IsReadOnly(Span span, bool isEdit)
        {
            throw new NotImplementedException();
        }

        public bool IsReadOnly(Span span)
        {
            throw new NotImplementedException();
        }

        public bool IsReadOnly(int position, bool isEdit)
        {
            throw new NotImplementedException();
        }

        public bool IsReadOnly(int position)
        {
            throw new NotImplementedException();
        }

        public event EventHandler PostChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<SnapshotSpanEventArgs> ReadOnlyRegionsChanged
        {
            add { }
            remove { }
        }

        public ITextSnapshot Replace(Span replaceSpan, string replaceWith)
        {
            throw new NotImplementedException();
        }

        public void TakeThreadOwnership()
        {
            throw new NotImplementedException();
        }

        public PropertyCollection Properties => throw new NotImplementedException();
    }


    internal class FakeSnapshot : ITextSnapshot
    {
        private readonly List<FakeLine> _Lines;
        private readonly string _Source;

        public FakeSnapshot(string source)
        {
            _Source = source.Replace("\\t", "    ").Replace("\\n", "\n");
            if (!_Source.EndsWith("\r\n")) _Source += "\r\n";

            _Lines = new List<FakeLine>();
            for (int line = 0, start = 0, end = _Source.IndexOf('\n');
                end >= start;
                start = end + 1, end = _Source.IndexOf('\n', start), line += 1)
                _Lines.Add(new FakeLine(this, _Source.Substring(start, end - start), line, start));
        }

        public void CopyTo(int sourceIndex, char[] destination, int destinationIndex, int count)
        {
            _Source.CopyTo(sourceIndex, destination, destinationIndex, count);
        }

        public ITextSnapshotLine GetLineFromLineNumber(int lineNumber)
        {
            return _Lines[lineNumber];
        }

        public ITextSnapshotLine GetLineFromPosition(int position)
        {
            return GetLineFromLineNumber(GetLineNumberFromPosition(position));
        }

        public int GetLineNumberFromPosition(int position)
        {
            for (int i = 1; i < LineCount; ++i)
            {
                if (position < _Lines[i].Start)
                    return i - 1;
                position -= _Lines[i].Start;
            }

            return -1;
        }

        public string GetText()
        {
            return _Source;
        }

        public string GetText(int startIndex, int length)
        {
            return _Source.Substring(startIndex, length);
        }

        public string GetText(Span span)
        {
            return GetText(span.Start, span.Length);
        }

        public int Length => _Source.Length;
        public int LineCount => _Lines.Count;

        public IEnumerable<ITextSnapshotLine> Lines => _Lines;
        public ITextBuffer TextBuffer => new FakeTextBuffer {CurrentSnapshot = this};

        public char[] ToCharArray(int startIndex, int length)
        {
            return _Source.ToCharArray(startIndex, length);
        }

        public void Write(TextWriter writer)
        {
            writer.Write(GetText());
        }

        public void Write(TextWriter writer, Span span)
        {
            writer.Write(GetText(span));
        }

        public char this[int position] => _Source[position];

        public IContentType ContentType => new FakeContentType("CSharp");

        public ITrackingPoint CreateTrackingPoint(int position, PointTrackingMode trackingMode,
            TrackingFidelityMode trackingFidelity)
        {
            throw new NotImplementedException();
        }

        public ITrackingPoint CreateTrackingPoint(int position, PointTrackingMode trackingMode)
        {
            throw new NotImplementedException();
        }

        public ITrackingSpan CreateTrackingSpan(int start, int length, SpanTrackingMode trackingMode,
            TrackingFidelityMode trackingFidelity)
        {
            throw new NotImplementedException();
        }

        public ITrackingSpan CreateTrackingSpan(int start, int length, SpanTrackingMode trackingMode)
        {
            throw new NotImplementedException();
        }

        public ITrackingSpan CreateTrackingSpan(Span span, SpanTrackingMode trackingMode,
            TrackingFidelityMode trackingFidelity)
        {
            throw new NotImplementedException();
        }

        public ITrackingSpan CreateTrackingSpan(Span span, SpanTrackingMode trackingMode)
        {
            throw new NotImplementedException();
        }

        public ITextVersion Version => throw new NotImplementedException();
    }
}