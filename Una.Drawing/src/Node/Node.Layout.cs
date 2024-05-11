﻿/* Una.Drawing                                                 ____ ___
 *   A declarative drawing library for FFXIV.                 |    |   \____ _____        ____                _
 *                                                            |    |   /    \\__  \      |    \ ___ ___ _ _ _|_|___ ___
 * By Una. Licensed under AGPL-3.                             |    |  |   |  \/ __ \_    |  |  |  _| .'| | | | |   | . |
 * https://github.com/una-xiv/drawing                         |______/|___|  (____  / [] |____/|_| |__,|_____|_|_|_|_  |
 * ----------------------------------------------------------------------- \/ --- \/ ----------------------------- |__*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;

namespace Una.Drawing;

public partial class Node
{
    public double LastReflowTime { get; private set; }

    private readonly Dictionary<Anchor.AnchorPoint, List<Node>> _anchorToChildNodes = [];
    private readonly Dictionary<Node, Anchor.AnchorPoint>       _childNodeToAnchor  = [];
    private readonly Stopwatch                                  _stopwatch          = new();

    private bool  _mustReflow = true;
    private bool  _isReflowing;
    private Point _position = new(0, 0);

    public void Reflow(Point? position = null)
    {
        _stopwatch.Restart();

        if (_mustReflow) {
            ComputeBoundingBox();
            ComputeStretchedNodeSizes();
        }

        if (_mustReflow || _position != position) {
            _position = position ?? new(0, 0);
            ComputeBoundingRects(_position);
        }

        _mustReflow = false;
        _stopwatch.Stop();

        LastReflowTime = _stopwatch.Elapsed.TotalMilliseconds;
    }

    #region Reflow Stage #1

    /// <summary>
    /// <para>
    /// Reflow stage #1: Compute the bounding box of this node.
    /// </para>
    /// <para>
    /// This method ensures that the <see cref="NodeBounds.ContentSize"/>,
    /// <see cref="NodeBounds.PaddingSize"/> and <see cref="NodeBounds.MarginSize"/>
    /// of this node are computed correctly, except for nodes that are supposed
    /// to be stretched (See <see cref="Style.Stretch"/>).
    /// </para>
    /// </summary>
    private void ComputeBoundingBox()
    {
        foreach (Node child in _childNodes) {
            child.ComputeBoundingBox();
        }

        ComputeNodeSize();
    }

    /// <summary>
    /// Computes the size of this node based on its own value and the size of
    /// the child nodes, if any.
    /// </summary>
    private void ComputeNodeSize()
    {
        if (!_mustReflow) return;

        // Always compute the content size from text, since it also prepares the
        // text content for rendering.
        Size contentSize = ComputeContentSizeFromText();

        if (false == Style.Size.IsFixed) {
            Size childSpan = ComputeContentSizeFromChildren();

            Bounds.ContentSize = new(
                Math.Max(contentSize.Width,  childSpan.Width),
                Math.Max(contentSize.Height, childSpan.Height)
            );
        }

        Bounds.PaddingSize = Bounds.ContentSize + _style.Padding.Size;

        // Readjust the content size based on the configured size constraints.
        if (Style.Size.Width > 0) Bounds.ContentSize.Width   = Bounds.PaddingSize.Width  = Style.Size.Width;
        if (Style.Size.Height > 0) Bounds.ContentSize.Height = Bounds.PaddingSize.Height = Style.Size.Height;

        Bounds.MarginSize = Bounds.PaddingSize + _style.Margin.Size;
    }

    /// <summary>
    /// Computes the content (inner) size of this node based on the size of
    /// its child nodes.
    /// </summary>
    private Size ComputeContentSizeFromChildren()
    {
        Size result = new();

        foreach (List<Node> childNodes in _anchorToChildNodes.Values) {
            var width  = 0;
            var height = 0;

            foreach (Node childNode in childNodes) {
                if (!childNode.Style.IsVisible) continue;

                switch (Style.Flow) {
                    case Flow.Horizontal:
                        width  += childNode.OuterWidth + Style.Gap;
                        height =  Math.Max(result.Height, childNode.OuterHeight);
                        break;
                    case Flow.Vertical:
                        width  =  Math.Max(result.Width, childNode.OuterWidth);
                        height += childNode.OuterHeight + Style.Gap;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            if (Style.Flow == Flow.Horizontal && width > 0) width -= Style.Gap;
            if (Style.Flow == Flow.Vertical && height > 0) height -= Style.Gap;

            result.Width  = Math.Max(result.Width,  width);
            result.Height = Math.Max(result.Height, height);
        }

        return result;
    }

    #endregion

    #region Reflow Stage #2

    private void ComputeStretchedNodeSizes()
    {
        // Start depth-first traversal of the node tree.
        foreach (Node child in _childNodes) {
            child.ComputeStretchedNodeSizes();
        }

        if (Style.Stretch && ParentNode is not null) {
            // Adjust the size of this node to match the size of the parent node.
            if (Style.Size.Width == 0 && ParentNode.Style.Flow == Flow.Vertical)
                Bounds.ContentSize.Width = ParentNode.InnerWidth;

            if (Style.Size.Height == 0 && ParentNode.Style.Flow == Flow.Horizontal)
                Bounds.ContentSize.Height = ParentNode.InnerHeight;

            Bounds.PaddingSize = Bounds.ContentSize.Copy();
            Bounds.MarginSize  = Bounds.PaddingSize + _style.Margin.Size;
        }
    }

    #endregion

    #region Reflow Stage #3

    /// <summary>
    /// Computes the bounding rectangles that define the position and size of
    /// each child node within this node recursively.
    /// </summary>
    private void ComputeBoundingRects(Point position)
    {
        Bounds.MarginRect.X1 = position.X;
        Bounds.MarginRect.Y1 = position.Y;
        Bounds.MarginRect.X2 = Bounds.MarginRect.X1 + OuterWidth;
        Bounds.MarginRect.Y2 = Bounds.MarginRect.Y1 + OuterHeight;

        Bounds.PaddingRect.X1 = Bounds.MarginRect.X1 + _style.Margin.Left;
        Bounds.PaddingRect.Y1 = Bounds.MarginRect.Y1 + _style.Margin.Top;
        Bounds.PaddingRect.X2 = Bounds.MarginRect.X2 - _style.Margin.Right;
        Bounds.PaddingRect.Y2 = Bounds.MarginRect.Y2 - _style.Margin.Bottom;

        Bounds.ContentRect.X1 = Bounds.PaddingRect.X1 + _style.Padding.Left;
        Bounds.ContentRect.Y1 = Bounds.PaddingRect.Y1 + _style.Padding.Top;
        Bounds.ContentRect.X2 = Bounds.PaddingRect.X2 - _style.Padding.Right;
        Bounds.ContentRect.Y2 = Bounds.PaddingRect.Y2 - _style.Padding.Bottom;

        int originX = Bounds.ContentRect.X1;
        int originY = Bounds.ContentRect.Y1;

        foreach (Anchor.AnchorPoint anchorPoint in _anchorToChildNodes.Keys) {
            List<Node> childNodes     = _anchorToChildNodes[anchorPoint];
            Size       maxChildSize   = GetMaxSizeOfChildren(childNodes);
            Size       totalChildSize = GetTotalSizeOfChildren(childNodes);
            Anchor     anchor         = new(anchorPoint);

            int x = originX;
            int y = originY;

            if (anchor.IsCenter) {
                x += InnerWidth / 2 - (Style.Flow == Flow.Horizontal ? totalChildSize.Width : maxChildSize.Width) / 2;
            }

            if (anchor.IsRight) x += InnerWidth;

            if (anchor.IsMiddle) {
                y += (InnerHeight / 2)
                    - (Style.Flow == Flow.Horizontal ? maxChildSize.Height : totalChildSize.Height) / 2;
            }

            if (anchor.IsBottom) y += Height;

            Node lastNode = childNodes.Last();

            foreach (Node childNode in childNodes) {
                var xOffset = 0;
                var yOffset = 0;

                if (anchor.IsMiddle) {
                    yOffset = ((maxChildSize.Height - childNode.OuterHeight) / 2) - Style.Padding.Top;
                } else if (anchor.IsBottom) {
                    yOffset -= (childNode.OuterHeight) + Style.Padding.VerticalSize;
                }

                if (anchor.IsCenter) {
                    xOffset = -Style.Padding.Left;
                } else if (anchor.IsRight) {
                    xOffset -= (childNode.OuterWidth) + Style.Padding.HorizontalSize;
                }

                childNode.ComputeBoundingRects(new(x + xOffset, y + yOffset));

                if (childNode == lastNode) break;

                switch (Style.Flow) {
                    case Flow.Horizontal:
                        x = anchor.IsRight
                            ? x - childNode.OuterWidth
                            : x + childNode.OuterWidth;

                        if (lastNode != childNode) {
                            x += anchor.IsRight ? -Style.Gap : Style.Gap;
                        }

                        break;
                    case Flow.Vertical:
                        y = anchor.IsBottom
                            ? y - childNode.OuterHeight
                            : y + childNode.OuterHeight;

                        if (lastNode != childNode) {
                            y += anchor.IsTop ? Style.Gap : -Style.Gap;
                        }

                        break;
                    default:
                        throw new ArgumentOutOfRangeException($"Unknown flow direction '{Style.Flow}'.");
                }
            }
        }

        _mustReflow = false;
    }

    private Size GetMaxSizeOfChildren(IReadOnlyCollection<Node> nodes)
    {
        if (nodes.Count == 0) return new();

        int width  = nodes.Max(node => node.OuterWidth);
        int height = nodes.Max(node => node.OuterHeight);

        if (Style.Flow == Flow.Horizontal) {
            width += Style.Gap * Math.Max(0, nodes.Count - 1);
        } else {
            height += Style.Gap * Math.Max(0, nodes.Count - 1);
        }

        return new(width, height);
    }

    private Size GetTotalSizeOfChildren(IReadOnlyCollection<Node> nodes)
    {
        if (nodes.Count == 0) return new();

        int width  = nodes.Sum(node => node.OuterWidth);
        int height = nodes.Sum(node => node.OuterHeight);

        if (Style.Flow == Flow.Horizontal) {
            width += Style.Gap * Math.Max(0, nodes.Count - 1);
        } else {
            height += Style.Gap * Math.Max(0, nodes.Count - 1);
        }

        return new(width, height);
    }

    #endregion
}
