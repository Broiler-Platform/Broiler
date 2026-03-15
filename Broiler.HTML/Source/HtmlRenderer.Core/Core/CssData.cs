using System;
using System.Collections.Generic;
using TheArtOfDev.HtmlRenderer.Core.Entities;

namespace TheArtOfDev.HtmlRenderer.Core;

public sealed class CssData
{
    private static readonly List<CssBlock> _emptyArray = [];
    private readonly Dictionary<string, Dictionary<string, List<CssBlock>>> _mediaBlocks = new(StringComparer.InvariantCultureIgnoreCase);

    internal CssData() => _mediaBlocks.Add("all", new Dictionary<string, List<CssBlock>>(StringComparer.InvariantCultureIgnoreCase));

    internal IDictionary<string, Dictionary<string, List<CssBlock>>> MediaBlocks => _mediaBlocks;

    public bool ContainsCssBlock(string className, string media = "all") => _mediaBlocks.TryGetValue(media, out Dictionary<string, List<CssBlock>> mid) && mid.ContainsKey(className);

    public IEnumerable<CssBlock> GetCssBlock(string className, string media = "all")
    {
        List<CssBlock> block = null;

        if (_mediaBlocks.TryGetValue(media, out Dictionary<string, List<CssBlock>> mid))
            mid.TryGetValue(className, out block);

        return block ?? _emptyArray;
    }

    public void AddCssBlock(string media, CssBlock cssBlock)
    {
        if (!_mediaBlocks.TryGetValue(media, out Dictionary<string, List<CssBlock>> mid))
        {
            mid = new Dictionary<string, List<CssBlock>>(StringComparer.InvariantCultureIgnoreCase);
            _mediaBlocks.Add(media, mid);
        }

        if (!mid.TryGetValue(cssBlock.Class, out List<CssBlock> list))
        {
            var list2 = new List<CssBlock> { cssBlock };
            mid[cssBlock.Class] = list2;
        }
        else
        {
            bool merged = false;
            foreach (var block in list)
            {
                if (block.EqualsSelector(cssBlock))
                {
                    merged = true;
                    block.Merge(cssBlock);
                    break;
                }
            }

            if (!merged)
            {
                // general block must be first
                if (cssBlock.Selectors == null)
                    list.Insert(0, cssBlock);
                else
                    list.Add(cssBlock);
            }
        }
    }

    public void Combine(CssData other)
    {
        ArgumentNullException.ThrowIfNull(other);

        // for each media block
        foreach (var mediaBlock in other.MediaBlocks)
        {
            // for each css class in the media block
            foreach (var bla in mediaBlock.Value)
            {
                // for each css block of the css class
                foreach (var cssBlock in bla.Value)
                {
                    // combine with this
                    AddCssBlock(mediaBlock.Key, cssBlock);
                }
            }
        }
    }

    public CssData Clone()
    {
        var clone = new CssData();
        foreach (var mid in _mediaBlocks)
        {
            var cloneMid = new Dictionary<string, List<CssBlock>>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var blocks in mid.Value)
            {
                var cloneList = new List<CssBlock>();
                foreach (var cssBlock in blocks.Value)
                {
                    cloneList.Add(cssBlock.Clone());
                }
                cloneMid[blocks.Key] = cloneList;
            }
            clone._mediaBlocks[mid.Key] = cloneMid;
        }
        return clone;
    }
}