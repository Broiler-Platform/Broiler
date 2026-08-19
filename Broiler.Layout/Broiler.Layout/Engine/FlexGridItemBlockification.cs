using Broiler.CSS;

namespace Broiler.Layout.Engine;

/// <summary>
/// CSS Display 3 §2.7 <em>blockification</em>: a flex or grid container's in-flow children are
/// flex/grid items, and an item's computed <c>display</c> is its <em>blockified</em> display —
/// <c>inline</c> becomes <c>block</c>, <c>inline-block</c> becomes <c>block</c>,
/// <c>inline-table</c> becomes <c>table</c>, and so on.
/// </summary>
/// <remarks>
/// <para>
/// Nothing performed that step, and the consequence was not a mis-sized item but a missing one.
/// <see cref="CssBox.PerformLayout"/> routes a box down the block path only when it satisfies the
/// block predicate; a child that is still <c>display: inline</c> takes the else-branch, which
/// copies a sibling's position and returns without ever resolving a size. The flex pass then
/// arranges a zero-sized box. Every inline-level child of a flex container — the overwhelmingly
/// common case, since <c>&lt;a&gt;</c> and <c>&lt;span&gt;</c> are what toolbars are built out of —
/// therefore laid out as nothing at all.
/// </para>
/// <para>
/// On <c>www.mediawiki.org</c> that is the whole Vector 2022 header: the hamburger button, the
/// wordmark link, the search, language and appearance buttons and the account links are
/// <c>&lt;a&gt;</c>/<c>&lt;span&gt;</c> children of flex containers, so the header rendered empty
/// except for the parts that happened to be block-level already.
/// </para>
/// <para>
/// The pass runs with the other box fix-ups, before the inline/block corrections, so a blockified
/// item takes part in them as the block-level box it has become — in particular it no longer
/// looks like inline content that the block-inside-inline split should wrap.
/// </para>
/// </remarks>
internal static class FlexGridItemBlockification
{
    /// <summary>
    /// Blockifies the flex/grid items under <paramref name="root"/>. Idempotent: a tree whose
    /// items are already block-level is left untouched.
    /// </summary>
    internal static void Generate(CssBox root)
    {
        if (root == null)
            return;

        Blockify(root);
    }

    private static void Blockify(CssBox box)
    {
        bool blockifiesChildren = IsFlexOrGridContainer(box.Display);

        for (int i = 0; i < box.Boxes.Count; i++)
        {
            CssBox child = box.Boxes[i];

            if (blockifiesChildren && IsInFlowItem(child))
            {
                // CSS Flexbox §3 / CSS Grid §6: `float` and `clear` have no effect on an item.
                // Neutralising them here, before layout, is what makes the child an item at all:
                // a box left with `float: left` is taken out of flow by the block pass, so the
                // container sizes as though the item were not there.
                child.Float = CssConstants.None;
                child.Clear = CssConstants.None;
                child.Display = BlockifiedDisplay(child.Display);
            }

            Blockify(child);
        }
    }

    private static bool IsFlexOrGridContainer(string display) =>
        display is "flex" or "inline-flex" or "grid" or "inline-grid";

    /// <summary>
    /// CSS Display 3 §2.7 / CSS Flexbox §4: only in-flow children become items. A
    /// <c>display: none</c> child generates no box, and an absolutely positioned one is not an
    /// item at all (it is blockified in its own right, by the out-of-flow rule, where it already
    /// is). A <c>display: contents</c> child is not a box either — its children are the items —
    /// so blockifying it would give the box a formatting role the spec says it does not have.
    /// <para>
    /// A <em>floated</em> child is in flow here, and deliberately so: CSS Flexbox §3 says
    /// <c>float</c> does not take a flex item out of flow, so it is an item like any other.
    /// </para>
    /// </summary>
    private static bool IsInFlowItem(CssBox child) =>
        child.Display != CssConstants.None
        && child.Display != "contents"
        && child.Position is not (CssConstants.Absolute or CssConstants.Fixed);

    /// <summary>
    /// The blockified equivalent of a computed <c>display</c>: the inline-level displays map to
    /// their block-level counterparts and everything else — including a display that is already
    /// block-level, and the layout-internal table displays, which blockify to themselves — is
    /// returned unchanged.
    /// </summary>
    /// <remarks>
    /// An anonymous text run carries no display of its own. It is inline-level content, and the
    /// spec wraps such a run in an anonymous block; the engine's own inline/block correction does
    /// exactly that a moment later, so leaving the empty display alone lets that pass handle it
    /// rather than turning a text run into a block box with no text semantics.
    /// </remarks>
    private static string BlockifiedDisplay(string display) => display switch
    {
        CssConstants.Inline or CssConstants.InlineBlock or "flow-root" => CssConstants.Block,
        "inline-flex" => "flex",
        "inline-grid" => "grid",
        CssConstants.InlineTable => CssConstants.Table,
        // A list item stays a list item; blockification only changes the outer display type, and
        // `list-item` is already block-level on the outside.
        _ => display,
    };
}
