namespace HtmlRenderer.Image.Tests;

/// <summary>
/// Provides all CSS2 chapter test HTML snippets for differential verification.
/// Each entry is a (chapter, testName, html) tuple extracted from
/// Css2Chapter6Tests, Css2Chapter9Tests, Css2Chapter10Tests, Css2Chapter12Tests,
/// Css2Chapter13Tests, Css2Chapter15Tests, Css2Chapter16Tests, and Css2Chapter17Tests.
/// </summary>
internal static class Css2TestSnippets
{
    /// <summary>CSS 2.1 Chapter 9 test snippets (50 tests).</summary>
    internal static readonly (string Name, string Html)[] Chapter9 =
    [
        ("S9_1_1_Viewport_InitialContainingBlock", @"<div style='width:400px;height:50px;background-color:blue;'></div>"),
        ("S9_1_2_ContainingBlock_NestedWidth", @"<div style='width:400px;'>
                <div style='width:50%;height:30px;background-color:red;'></div>
              </div>"),
        ("S9_2_1_BlockBoxes_StackVertically", @"<div style='width:400px;'>
                <div style='height:50px;background-color:red;'></div>
                <div style='height:50px;background-color:blue;'></div>
              </div>"),
        ("S9_2_1_1_AnonymousBlockBoxes", @"<div style='width:300px;'>
                Some inline text
                <div style='height:30px;background-color:green;'></div>
                More inline text
              </div>"),
        ("S9_2_2_InlineBoxes_SideBySide", @"<div style='width:400px;'>
                <span>Hello</span> <span>World</span>
              </div>"),
        ("S9_2_2_InlineBlock_AtomicInline", @"<div style='width:400px;'>
                <span style='display:inline-block;width:100px;height:40px;background-color:red;'></span>
                <span style='display:inline-block;width:100px;height:40px;background-color:blue;'></span>
              </div>"),
        ("S9_2_4_DisplayBlock", @"<span style='display:block;width:200px;height:50px;background-color:red;'></span>"),
        ("S9_2_4_DisplayInline", @"<div style='width:400px;'>
                <span>Inline element</span>
              </div>"),
        ("S9_2_4_DisplayNone_RemovedFromLayout", @"<div style='width:300px;'>
                <div style='height:50px;background-color:red;'></div>
                <div style='display:none;height:100px;background-color:green;'></div>
                <div style='height:50px;background-color:blue;'></div>
              </div>"),
        ("S9_2_4_DisplayNone_Vs_VisibilityHidden_htmlNone", @"<div style='width:300px;'>
                <div style='height:50px;'></div>
                <div style='display:none;height:100px;'></div>
                <div style='height:50px;'></div>
              </div>"),
        ("S9_2_4_DisplayNone_Vs_VisibilityHidden_htmlHidden", @"<div style='width:300px;'>
                <div style='height:50px;'></div>
                <div style='visibility:hidden;height:100px;'></div>
                <div style='height:50px;'></div>
              </div>"),
        ("S9_2_4_DisplayListItem", @"<ul style='width:300px;'>
                <li>Item one</li>
                <li>Item two</li>
              </ul>"),
        ("S9_2_4_DisplayTable", @"<table style='width:300px;border:1px solid black;'>
                <tr><td>Cell 1</td><td>Cell 2</td></tr>
              </table>"),
        ("S9_3_1_PositionStatic_DefaultNormalFlow", @"<div style='width:400px;'>
                <div style='height:50px;background-color:red;'></div>
                <div style='height:50px;background-color:blue;'></div>
              </div>"),
        ("S9_3_1_PositionRelative_OffsetFromNormalFlow", @"<div style='width:400px;'>
                <div style='height:50px;background-color:red;'></div>
                <div style='position:relative;top:20px;left:30px;height:50px;background-color:blue;'></div>
                <div style='height:50px;background-color:green;'></div>
              </div>"),
        ("S9_3_1_PositionAbsolute_RemovedFromFlow", @"<div style='width:400px;position:relative;'>
                <div style='height:50px;background-color:red;'></div>
                <div style='position:absolute;top:10px;left:10px;width:100px;height:100px;background-color:rgba(0,0,255,0.5);'></div>
                <div style='height:50px;background-color:green;'></div>
              </div>"),
        ("S9_3_1_PositionFixed", @"<div style='width:400px;'>
                <div style='position:fixed;top:0;left:0;width:100px;height:30px;background-color:red;'></div>
                <div style='height:50px;background-color:blue;'></div>
              </div>"),
        ("S9_3_2_BoxOffsets_TopLeft", @"<div style='width:400px;position:relative;'>
                <div style='position:absolute;top:25px;left:50px;width:100px;height:100px;background-color:red;'></div>
              </div>"),
        ("S9_4_1_BFC_VerticalLayout", @"<div style='width:400px;overflow:hidden;'>
                <div style='height:40px;background-color:red;'></div>
                <div style='height:40px;background-color:blue;'></div>
                <div style='height:40px;background-color:green;'></div>
              </div>"),
        ("S9_4_1_BFC_LeftEdgeTouchesContainingBlock", @"<div style='width:300px;padding:10px;'>
                <div style='height:30px;background-color:red;'></div>
              </div>"),
        ("S9_4_1_BFC_EstablishedByOverflowHidden", @"<div style='width:400px;overflow:hidden;'>
                <div style='float:left;width:100px;height:80px;background-color:red;'></div>
                <div style='float:right;width:100px;height:60px;background-color:blue;'></div>
              </div>"),
        ("S9_4_2_IFC_HorizontalInlineLayout", @"<div style='width:400px;'>
                <span>First</span> <span>Second</span> <span>Third</span>
              </div>"),
        ("S9_4_2_IFC_LineBoxWrapping", @"<div style='width:100px;'>
                The quick brown fox jumps over the lazy dog.
              </div>"),
        ("S9_4_3_RelativePositioning_NoEffectOnSiblings", @"<div style='width:400px;'>
                <div style='height:40px;background-color:red;'></div>
                <div style='position:relative;top:20px;height:40px;background-color:blue;'></div>
                <div style='height:40px;background-color:green;'></div>
              </div>"),
        ("S9_4_1_MarginCollapsing_Siblings", @"<div style='width:300px;'>
                <div style='margin-bottom:20px;height:30px;background-color:red;'></div>
                <div style='margin-top:15px;height:30px;background-color:blue;'></div>
              </div>"),
        ("S9_5_1_FloatLeft_TouchesContainingBlockEdge", @"<div style='width:400px;'>
                <div style='float:left;width:100px;height:50px;background-color:red;'></div>
              </div>"),
        ("S9_5_1_FloatRight", @"<div style='width:400px;'>
                <div style='float:right;width:100px;height:50px;background-color:red;'></div>
              </div>"),
        ("S9_5_1_FloatRule2_SuccessiveLeftFloats", @"<div style='width:400px;'>
                <div style='float:left;width:100px;height:50px;background-color:red;'></div>
                <div style='float:left;width:100px;height:50px;background-color:blue;'></div>
                <div style='float:left;width:100px;height:50px;background-color:green;'></div>
              </div>"),
        ("S9_5_1_FloatRule4_TopNotHigherThanContainingBlock", @"<div style='width:400px;padding-top:20px;'>
                <div style='float:left;width:100px;height:50px;background-color:red;'></div>
              </div>"),
        ("S9_5_1_FloatRule5_TopNotHigherThanEarlierFloat", @"<div style='width:400px;'>
                <div style='float:left;width:200px;height:80px;background-color:red;'></div>
                <div style='float:left;width:200px;height:50px;background-color:blue;'></div>
              </div>"),
        ("S9_5_1_FloatRule7_WrapsWhenExceedingWidth", @"<div style='width:300px;'>
                <div style='float:left;width:150px;height:50px;background-color:red;'></div>
                <div style='float:left;width:200px;height:50px;background-color:blue;'></div>
              </div>"),
        ("S9_5_1_FloatRule8_9_PlacedAsHighAndFarAsPossible", @"<div style='width:400px;'>
                <div style='float:left;width:100px;height:50px;background-color:red;'></div>
                <div style='float:right;width:100px;height:50px;background-color:blue;'></div>
              </div>"),
        ("S9_5_1_ContentFlowsAroundFloat", @"<div style='width:300px;'>
                <div style='float:left;width:80px;height:60px;background-color:red;'></div>
                <span>Text that should wrap around the floated element on the left side of the container.</span>
              </div>"),
        ("S9_5_1_Float_IsBlockLevel", @"<div style='width:400px;'>
                <span style='float:left;width:100px;height:50px;background-color:red;'>Floated span</span>
              </div>"),
        ("S9_5_2_ClearLeft", @"<div style='width:400px;'>
                <div style='float:left;width:100px;height:50px;background-color:red;'></div>
                <div style='clear:left;height:30px;background-color:blue;'></div>
              </div>"),
        ("S9_5_2_ClearRight", @"<div style='width:400px;'>
                <div style='float:right;width:100px;height:50px;background-color:red;'></div>
                <div style='clear:right;height:30px;background-color:blue;'></div>
              </div>"),
        ("S9_5_2_ClearBoth", @"<div style='width:400px;'>
                <div style='float:left;width:100px;height:80px;background-color:red;'></div>
                <div style='float:right;width:100px;height:50px;background-color:green;'></div>
                <div style='clear:both;height:30px;background-color:blue;'></div>
              </div>"),
        ("S9_6_AbsolutePositioning_RemovedFromFlow", @"<div style='width:400px;position:relative;'>
                <div style='height:50px;background-color:red;'></div>
                <div style='position:absolute;top:0;left:0;width:50px;height:50px;background-color:blue;'></div>
                <div style='height:50px;background-color:green;'></div>
              </div>"),
        ("S9_6_AbsolutePositioning_ContainingBlockIsPositionedAncestor", @"<div style='width:400px;position:relative;padding:20px;'>
                <div style='position:absolute;top:10px;left:10px;width:80px;height:80px;background-color:red;'></div>
              </div>"),
        ("S9_6_1_FixedPositioning", @"<div style='width:400px;'>
                <div style='position:fixed;top:5px;left:5px;width:80px;height:30px;background-color:red;'></div>
                <div style='height:100px;background-color:blue;'></div>
              </div>"),
        ("S9_7_DisplayNone_IgnoresPositionAndFloat", @"<div style='width:400px;'>
                <div style='display:none;position:absolute;float:left;width:100px;height:100px;background-color:red;'></div>
                <div style='height:50px;background-color:blue;'></div>
              </div>"),
        ("S9_7_FloatAdjustsDisplay", @"<div style='width:400px;'>
                <span style='float:left;width:100px;height:50px;background-color:red;'>Block-ified</span>
                <div style='height:50px;background-color:blue;'></div>
              </div>"),
        ("S9_8_ComparisonExample_AllPositioningSchemes", @"<div style='width:400px;position:relative;'>
                <div style='height:30px;background-color:#ccc;'>Normal flow</div>
                <div style='position:relative;top:5px;left:5px;height:30px;background-color:#aaa;'>Relative</div>
                <div style='float:left;width:100px;height:50px;background-color:#888;'>Float</div>
                <div style='position:absolute;top:0;right:0;width:80px;height:80px;background-color:#666;'>Absolute</div>
                <div style='height:30px;background-color:#eee;'>After float</div>
              </div>"),
        ("S9_9_1_ZIndex_PositionedElements", @"<div style='width:400px;position:relative;'>
                <div style='position:absolute;z-index:1;top:10px;left:10px;width:100px;height:100px;background-color:red;'></div>
                <div style='position:absolute;z-index:2;top:30px;left:30px;width:100px;height:100px;background-color:blue;'></div>
              </div>"),
        ("S9_10_DirectionLtr_Default", @"<div style='width:300px;direction:ltr;'>
                <span>Left to right text</span>
              </div>"),
        ("S9_10_DirectionRtl", @"<div style='width:300px;direction:rtl;'>
                <span>Right to left text</span>
              </div>"),
        ("Pixel_FloatLeft_RendersAtLeftEdge", @"<body style='margin:0;padding:0;'>
                <div style='float:left;width:50px;height:50px;background-color:red;'></div>
              </body>"),
        ("Pixel_DisplayNone_ProducesNoOutput", @"<body style='margin:0;padding:0;'>
                <div style='display:none;width:50px;height:50px;background-color:red;'></div>
              </body>"),
        ("Pixel_BlockBoxes_StackVertically", @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:40px;background-color:red;'></div>
                <div style='width:100px;height:40px;background-color:blue;'></div>
              </body>"),
        ("Pixel_ClearBoth_MovesContentBelowFloats", @"<body style='margin:0;padding:0;'>
                <div style='float:left;width:100px;height:50px;background-color:red;'></div>
                <div style='clear:both;width:100px;height:50px;background-color:blue;'></div>
              </body>"),
    ];

    /// <summary>CSS 2.1 Chapter 10 test snippets (135 tests).</summary>
    internal static readonly (string Name, string Html)[] Chapter10 =
    [
        ("S10_1_RootElement_InitialContainingBlock", @"<div style='width:400px;height:50px;background-color:red;'></div>"),
        ("S10_1_RootElement_NarrowViewport", @"<div style='width:100%;height:30px;background-color:blue;'></div>"),
        ("S10_1_StaticPosition_ContainingBlockIsAncestorContentEdge", @"<div style='width:400px;padding:20px;'>
                <div style='width:50%;height:30px;background-color:red;'></div>
              </div>"),
        ("S10_1_RelativePosition_ContainingBlockSameAsStatic", @"<div style='width:400px;padding:10px;'>
                <div style='position:relative;top:5px;width:50%;height:30px;background-color:green;'></div>
              </div>"),
        ("S10_1_FixedPosition_ContainingBlockIsViewport", @"<div style='width:300px;'>
                <div style='position:fixed;top:0;left:0;width:100px;height:30px;background-color:red;'></div>
              </div>"),
        ("S10_1_AbsolutePosition_ContainingBlockIsPaddingEdge", @"<div style='position:relative;width:400px;padding:20px;'>
                <div style='position:absolute;top:0;left:0;width:50%;height:30px;background-color:red;'></div>
              </div>"),
        ("S10_1_AbsolutePosition_NoPositionedAncestor_UsesICB", @"<div style='width:300px;'>
                <div style='position:absolute;top:10px;left:10px;width:100px;height:40px;background-color:blue;'></div>
              </div>"),
        ("S10_1_Golden_NestedContainingBlocks", @"<div style='width:400px;padding:10px;'>
                <div style='width:50%;height:40px;background-color:red;'></div>
                <div style='width:75%;height:40px;background-color:blue;'></div>
              </div>"),
        ("S10_2_Width_ExplicitLength", @"<div style='width:250px;height:30px;background-color:red;'></div>"),
        ("S10_2_Golden_ExplicitWidth", @"<div style='width:300px;height:40px;background-color:green;'></div>"),
        ("S10_2_Width_Percentage", @"<div style='width:400px;'>
                <div style='width:25%;height:30px;background-color:blue;'></div>
              </div>"),
        ("S10_2_Width_Percentage_75", @"<div style='width:400px;'>
                <div style='width:75%;height:30px;background-color:red;'></div>
              </div>"),
        ("S10_2_Width_Auto_Block", @"<div style='width:400px;'>
                <div style='height:30px;background-color:green;'></div>
              </div>"),
        ("S10_2_Width_Auto_WithParentPadding", @"<div style='width:400px;padding:20px;'>
                <div style='height:30px;background-color:blue;'></div>
              </div>"),
        ("S10_2_Width_DoesNotApplyToInlineElements", @"<div style='width:400px;'>
                <span style='width:200px;background-color:red;'>Short</span>
              </div>"),
        ("S10_2_Width_DoesNotApplyToTableRows", @"<table style='width:300px;border-collapse:collapse;'>
                <tr style='width:100px;'><td>Cell</td></tr>
              </table>"),
        ("S10_2_Width_NegativeValueIgnored", @"<div style='width:400px;'>
                <div style='width:-50px;height:30px;background-color:red;'></div>
              </div>"),
        ("S10_3_1_InlineNonReplaced_WidthDoesNotApply", @"<div style='width:400px;'>
                <span style='width:300px;'>Some text</span>
              </div>"),
        ("S10_3_1_InlineNonReplaced_HorizontalMarginsApply", @"<div style='width:400px;'>
                <span style='margin-left:20px;margin-right:20px;background-color:red;'>Text</span>
              </div>"),
        ("S10_3_1_InlineNonReplaced_PaddingPushesContent", @"<div style='width:400px;'>
                <span style='padding-left:15px;padding-right:15px;border:2px solid black;background-color:yellow;'>Padded</span>
                <span style='background-color:lime;'>Next</span>
              </div>"),
        ("S10_3_1_Golden_InlineWithMarginsAndPadding", @"<div style='width:400px;'>
                <span style='margin:0 10px;padding:5px;border:1px solid black;background-color:yellow;'>Styled</span>
              </div>"),
        ("S10_3_2_InlineReplaced_ExplicitWidth", @"<div style='width:400px;'>
                <span style='display:inline-block;width:150px;height:50px;background-color:red;'></span>
              </div>"),
        ("S10_3_2_InlineReplaced_PercentageWidth", @"<div style='width:400px;'>
                <span style='display:inline-block;width:50%;height:40px;background-color:blue;'></span>
              </div>"),
        ("S10_3_2_InlineReplaced_AutoWidthShrinkToFit", @"<div style='width:400px;'>
                <span style='display:inline-block;background-color:green;'>Hello</span>
              </div>"),
        ("S10_3_2_InlineReplaced_AutoWidthNarrowerThanContainer", @"<div style='width:400px;'>
                <span style='display:inline-block;background-color:red;'>X</span>
              </div>"),
        ("S10_3_2_Golden_InlineBlockWidths", @"<div style='width:400px;'>
                <span style='display:inline-block;width:100px;height:30px;background-color:red;'></span>
                <span style='display:inline-block;width:50%;height:30px;background-color:blue;'></span>
              </div>"),
        ("S10_3_3_BlockConstraintEquation", @"<div style='width:400px;'>
                <div style='width:200px;margin-left:50px;margin-right:50px;
                            padding-left:20px;padding-right:20px;
                            border-left:10px solid black;border-right:10px solid black;
                            height:30px;background-color:red;'></div>
              </div>"),
        ("S10_3_3_Golden_BlockConstraintEquation", @"<div style='width:400px;'>
                <div style='width:300px;margin:0 auto;height:30px;background-color:green;'></div>
              </div>"),
        ("S10_3_3_OverConstrainedAutoMarginsBecome0", @"<div style='width:200px;'>
                <div style='width:300px;margin-left:auto;height:30px;background-color:red;'></div>
              </div>"),
        ("S10_3_3_OneAutoValue_MarginRight", @"<div style='width:400px;'>
                <div style='width:200px;margin-left:50px;margin-right:auto;height:30px;background-color:blue;'></div>
              </div>"),
        ("S10_3_3_OneAutoValue_MarginLeft", @"<div style='width:400px;'>
                <div style='width:200px;margin-left:auto;margin-right:50px;height:30px;background-color:red;'></div>
              </div>"),
        ("S10_3_3_AutoWidth_FillsRemainingSpace", @"<div style='width:400px;'>
                <div style='margin-left:30px;margin-right:30px;height:30px;background-color:green;'></div>
              </div>"),
        ("S10_3_3_BothMarginsAuto_Centering", @"<div style='width:400px;'>
                <div style='width:200px;margin-left:auto;margin-right:auto;height:30px;background-color:blue;'></div>
              </div>"),
        ("Pixel_S10_3_3_BlockWidth_RendersCorrectly", @"<body style='margin:0;padding:0;'>
                <div style='width:200px;height:40px;background-color:red;'></div>
                <div style='width:200px;height:40px;background-color:blue;'></div>
              </body>"),
        ("S10_3_3_OverConstrained_MarginRightAdjusted", @"<div style='width:400px;'>
                <div style='width:300px;margin-left:50px;margin-right:200px;
                            height:30px;background-color:red;'></div>
              </div>"),
        ("S10_3_4_BlockReplaced_WidthAndMargins", @"<div style='width:400px;'>
                <div style='display:block;width:150px;margin-left:auto;margin-right:auto;
                            height:50px;background-color:red;'></div>
              </div>"),
        ("S10_3_4_Golden_BlockReplacedCentred", @"<div style='width:400px;'>
                <div style='width:200px;margin:0 auto;height:40px;background-color:blue;'></div>
              </div>"),
        ("S10_3_5_FloatAutoWidth_ShrinkToFit", @"<div style='width:400px;'>
                <div style='float:left;background-color:red;'>Short</div>
              </div>"),
        ("S10_3_5_FloatAutoWidth_NarrowerThanContainer", @"<div style='width:400px;'>
                <div style='float:left;background-color:blue;'>X</div>
              </div>"),
        ("S10_3_5_FloatAutoMargins_ComputeToZero", @"<div style='width:400px;'>
                <div style='float:left;width:200px;margin-left:auto;margin-right:auto;
                            height:40px;background-color:green;'></div>
              </div>"),
        ("S10_3_5_Golden_FloatShrinkToFit", @"<div style='width:400px;overflow:hidden;'>
                <div style='float:left;background-color:red;padding:5px;'>Float content</div>
                <div style='height:30px;background-color:blue;'></div>
              </div>"),
        ("S10_3_6_FloatReplaced_ExplicitWidth", @"<div style='width:400px;'>
                <div style='float:left;width:150px;height:50px;background-color:red;'></div>
              </div>"),
        ("S10_3_6_FloatReplaced_AutoMarginsZero", @"<div style='width:400px;'>
                <div style='float:right;width:100px;margin-left:auto;margin-right:auto;
                            height:40px;background-color:blue;'></div>
              </div>"),
        ("S10_3_7_AbsoluteConstraintEquation", @"<div style='position:relative;width:400px;height:100px;'>
                <div style='position:absolute;left:20px;right:20px;
                            height:40px;background-color:red;'></div>
              </div>"),
        ("S10_3_7_AllAutoValues_MarginsBecome0", @"<div style='position:relative;width:400px;height:100px;'>
                <div style='position:absolute;margin-left:auto;margin-right:auto;
                            height:40px;background-color:blue;'>Content</div>
              </div>"),
        ("S10_3_7_NoneAuto_OverConstrained", @"<div style='position:relative;width:400px;height:100px;'>
                <div style='position:absolute;left:50px;right:50px;width:400px;
                            height:40px;background-color:green;'></div>
              </div>"),
        ("S10_3_7_OneAutoValue_WidthAuto", @"<div style='position:relative;width:400px;height:100px;'>
                <div style='position:absolute;left:50px;right:50px;
                            height:40px;background-color:red;'></div>
              </div>"),
        ("S10_3_7_AutoWidth_ShrinkToFit", @"<div style='position:relative;width:400px;height:100px;'>
                <div style='position:absolute;left:10px;
                            height:40px;background-color:blue;'>Short</div>
              </div>"),
        ("S10_3_7_AutoMargins_SplitEqually", @"<div style='position:relative;width:400px;height:100px;'>
                <div style='position:absolute;left:0;right:0;width:200px;
                            margin-left:auto;margin-right:auto;
                            height:40px;background-color:green;'></div>
              </div>"),
        ("S10_3_7_Golden_AbsolutePositioned", @"<div style='position:relative;width:400px;height:100px;'>
                <div style='position:absolute;left:20px;right:20px;
                            height:50px;background-color:red;'></div>
              </div>"),
        ("S10_3_8_AbsoluteReplaced_ExplicitWidth", @"<div style='position:relative;width:400px;height:100px;'>
                <div style='position:absolute;left:10px;width:150px;
                            height:40px;background-color:red;'></div>
              </div>"),
        ("S10_3_8_AbsoluteReplaced_Margins", @"<div style='position:relative;width:400px;height:100px;'>
                <div style='position:absolute;left:0;right:0;width:200px;
                            margin-left:auto;margin-right:auto;
                            height:40px;background-color:blue;'></div>
              </div>"),
        ("S10_3_9_InlineBlockNonReplaced_AutoWidth_ShrinkToFit", @"<div style='width:400px;'>
                <span style='display:inline-block;background-color:red;'>Short text</span>
              </div>"),
        ("S10_3_9_InlineBlockNonReplaced_ExplicitWidth", @"<div style='width:400px;'>
                <span style='display:inline-block;width:180px;height:40px;background-color:blue;'></span>
              </div>"),
        ("S10_3_10_InlineBlockReplaced_ExplicitWidth", @"<div style='width:400px;'>
                <span style='display:inline-block;width:120px;height:40px;background-color:green;'></span>
              </div>"),
        ("S10_3_10_InlineBlockReplaced_PercentageWidth", @"<div style='width:400px;'>
                <span style='display:inline-block;width:30%;height:40px;background-color:red;'></span>
              </div>"),
        ("S10_4_MinWidth_Length", @"<div style='width:400px;'>
                <div style='width:50px;min-width:200px;height:30px;background-color:red;'></div>
              </div>"),
        ("S10_4_MinWidth_Percentage", @"<div style='width:400px;'>
                <div style='width:50px;min-width:50%;height:30px;background-color:blue;'></div>
              </div>"),
        ("S10_4_MaxWidth_Length", @"<div style='width:400px;'>
                <div style='max-width:200px;height:30px;background-color:green;'></div>
              </div>"),
        ("S10_4_MaxWidth_Percentage", @"<div style='width:400px;'>
                <div style='max-width:25%;height:30px;background-color:red;'></div>
              </div>"),
        ("S10_4_Algorithm_TentativeExceedsMax", @"<div style='width:400px;'>
                <div style='width:300px;max-width:150px;height:30px;background-color:blue;'></div>
              </div>"),
        ("S10_4_Algorithm_TentativeLessThanMin", @"<div style='width:400px;'>
                <div style='width:50px;min-width:200px;height:30px;background-color:green;'></div>
              </div>"),
        ("S10_4_NegativeValues_Ignored", @"<div style='width:400px;'>
                <div style='min-width:-50px;max-width:-100px;height:30px;background-color:red;'></div>
              </div>"),
        ("S10_4_DoesNotApplyToInline", @"<div style='width:400px;'>
                <span style='min-width:300px;max-width:50px;background-color:red;'>Text</span>
              </div>"),
        ("S10_4_Golden_MinMaxWidth", @"<div style='width:400px;'>
                <div style='width:50px;min-width:150px;height:30px;background-color:red;'></div>
                <div style='max-width:250px;height:30px;background-color:blue;'></div>
              </div>"),
        ("S10_5_Height_ExplicitLength", @"<div style='width:200px;height:150px;background-color:red;'></div>"),
        ("S10_5_Height_Percentage", @"<div style='width:200px;height:200px;'>
                <div style='height:50%;background-color:blue;'></div>
              </div>"),
        ("S10_5_Height_Percentage_25", @"<div style='width:200px;height:400px;'>
                <div style='height:25%;background-color:green;'></div>
              </div>"),
        ("S10_5_Height_Auto_DeterminedByContent", @"<div style='width:200px;'>
                <div style='height:60px;background-color:red;'></div>
                <div style='height:40px;background-color:blue;'></div>
              </div>"),
        ("S10_5_PercentageHeight_ContainingBlockAuto", @"<div style='width:200px;'>
                <div style='height:50%;background-color:red;'>Text</div>
              </div>"),
        ("S10_5_Height_DoesNotApplyToInline", @"<div style='width:300px;'>
                <span style='height:200px;background-color:red;'>Inline text</span>
              </div>"),
        ("S10_5_Height_NegativeValueIgnored", @"<div style='width:200px;height:-50px;background-color:red;'>Content</div>"),
        ("S10_5_Golden_Heights", @"<div style='width:200px;height:200px;'>
                <div style='height:50%;background-color:red;'></div>
                <div style='height:50px;background-color:blue;'></div>
              </div>"),
        ("S10_6_1_InlineNonReplaced_HeightDoesNotApply", @"<div style='width:400px;'>
                <span style='height:200px;background-color:red;'>Text content</span>
              </div>"),
        ("S10_6_1_InlineNonReplaced_HeightFromFontMetrics", @"<div style='width:400px;font-size:16px;'>
                <span style='background-color:yellow;'>Text</span>
              </div>"),
        ("S10_6_1_InlineNonReplaced_VerticalPaddingNoLineBoxEffect", @"<div style='width:400px;'>
                <span style='padding-top:20px;padding-bottom:20px;background-color:yellow;'>Padded</span>
              </div>"),
        ("S10_6_1_InlineNonReplaced_LineHeight_htmlNormal", @"<div style='width:400px;line-height:normal;'>
                <span>Text</span>
              </div>"),
        ("S10_6_1_InlineNonReplaced_LineHeight_htmlLarge", @"<div style='width:400px;line-height:40px;'>
                <span>Text</span>
              </div>"),
        ("S10_6_2_InlineReplaced_ExplicitHeight", @"<div style='width:400px;'>
                <span style='display:inline-block;width:100px;height:80px;background-color:red;'></span>
              </div>"),
        ("S10_6_2_InlineReplaced_AutoHeight", @"<div style='width:400px;'>
                <span style='display:inline-block;width:100px;background-color:blue;'>Hello</span>
              </div>"),
        ("S10_6_2_InlineReplaced_PercentageHeight", @"<div style='width:400px;height:200px;'>
                <span style='display:inline-block;width:100px;height:50%;background-color:green;'></span>
              </div>"),
        ("S10_6_3_BlockAutoHeight_FromChildren", @"<div style='width:300px;'>
                <div style='height:50px;background-color:red;'></div>
                <div style='height:70px;background-color:blue;'></div>
              </div>"),
        ("S10_6_3_BlockAutoHeight_FloatsDoNotContribute", @"<div style='width:300px;'>
                <div style='float:left;width:100px;height:200px;background-color:red;'></div>
              </div>"),
        ("S10_6_3_BlockAutoHeight_MarginCollapse", @"<div style='width:300px;'>
                <div style='margin-top:20px;margin-bottom:20px;height:50px;background-color:red;'></div>
              </div>"),
        ("S10_6_3_BlockAutoHeight_NoChildren_HeightIs0", @"<div style='width:300px;'></div>"),
        ("S10_6_3_Golden_BlockAutoHeight", @"<div style='width:300px;'>
                <div style='height:40px;background-color:red;'></div>
                <div style='height:60px;background-color:blue;'></div>
              </div>"),
        ("Pixel_S10_6_3_BlockAutoHeight", @"<body style='margin:0;padding:0;'>
                <div style='width:100px;'>
                    <div style='height:40px;background-color:red;'></div>
                    <div style='height:40px;background-color:blue;'></div>
                </div>
              </body>"),
        ("S10_6_4_AbsoluteHeight_Explicit", @"<div style='position:relative;width:300px;height:200px;'>
                <div style='position:absolute;top:10px;height:80px;width:100px;
                            background-color:red;'></div>
              </div>"),
        ("S10_6_4_AbsoluteHeight_TopBottom", @"<div style='position:relative;width:300px;height:200px;'>
                <div style='position:absolute;top:20px;bottom:20px;width:100px;
                            background-color:blue;'></div>
              </div>"),
        ("S10_6_4_AbsoluteHeight_AutoMargins", @"<div style='position:relative;width:300px;height:200px;'>
                <div style='position:absolute;top:0;bottom:0;height:100px;
                            margin-top:auto;margin-bottom:auto;width:100px;
                            background-color:green;'></div>
              </div>"),
        ("S10_6_5_AbsoluteReplaced_ExplicitHeight", @"<div style='position:relative;width:300px;height:200px;'>
                <div style='position:absolute;top:10px;width:100px;height:60px;
                            background-color:red;'></div>
              </div>"),
        ("S10_6_5_AbsoluteReplaced_WithMargins", @"<div style='position:relative;width:300px;height:200px;'>
                <div style='position:absolute;top:0;bottom:0;height:80px;
                            margin-top:auto;margin-bottom:auto;width:100px;
                            background-color:blue;'></div>
              </div>"),
        ("S10_6_6_InlineBlock_AutoHeightIncludesFloats", @"<div style='width:400px;'>
                <span style='display:inline-block;'>
                    <div style='float:left;width:50px;height:80px;background-color:red;'></div>
                    <span>Text</span>
                </span>
              </div>"),
        ("S10_6_6_OverflowHidden_AutoHeightIncludesFloats", @"<div style='width:300px;overflow:hidden;'>
                <div style='float:left;width:100px;height:100px;background-color:red;'></div>
              </div>"),
        ("S10_6_6_OverflowAuto_AutoHeightIncludesFloats", @"<div style='width:300px;overflow:auto;'>
                <div style='float:left;width:80px;height:120px;background-color:blue;'></div>
              </div>"),
        ("S10_6_6_Golden_OverflowHiddenWithFloat", @"<div style='width:300px;overflow:hidden;'>
                <div style='float:left;width:100px;height:80px;background-color:red;'></div>
                <div style='height:30px;background-color:blue;'></div>
              </div>"),
        ("S10_6_7_BFCRoot_AutoHeightIncludesFloats", @"<div style='width:300px;overflow:hidden;'>
                <div style='float:left;width:80px;height:150px;background-color:red;'></div>
                <div style='height:30px;background-color:blue;'></div>
              </div>"),
        ("S10_6_7_BFCRoot_FloatTallerThanContent", @"<div style='width:300px;overflow:hidden;'>
                <div style='float:left;width:80px;height:200px;background-color:red;'></div>
                <div style='height:50px;background-color:blue;'></div>
              </div>"),
        ("S10_6_7_BFCRoot_ContentTallerThanFloat", @"<div style='width:300px;overflow:hidden;'>
                <div style='float:left;width:80px;height:50px;background-color:red;'></div>
                <div style='height:200px;background-color:blue;'></div>
              </div>"),
        ("Pixel_S10_6_7_BFCRootIncludesFloat", @"<body style='margin:0;padding:0;'>
                <div style='width:200px;overflow:hidden;background-color:lime;'>
                    <div style='float:left;width:80px;height:100px;background-color:red;'></div>
                </div>
              </body>"),
        ("S10_7_MinHeight_Length", @"<div style='width:200px;height:20px;min-height:100px;background-color:red;'></div>"),
        ("S10_7_MinHeight_Percentage", @"<div style='width:200px;height:200px;'>
                <div style='height:20px;min-height:50%;background-color:blue;'></div>
              </div>"),
        ("S10_7_MaxHeight_Length", @"<div style='width:200px;height:300px;max-height:100px;background-color:green;'></div>"),
        ("S10_7_Algorithm_TentativeExceedsMax", @"<div style='width:200px;height:400px;max-height:150px;background-color:red;'></div>"),
        ("S10_7_NegativeValues_Ignored", @"<div style='width:200px;min-height:-50px;max-height:-100px;background-color:blue;'>Content</div>"),
        ("S10_7_Golden_MinMaxHeight", @"<div style='width:200px;'>
                <div style='height:20px;min-height:80px;background-color:red;'></div>
                <div style='height:300px;max-height:100px;background-color:blue;'></div>
              </div>"),
        ("S10_8_1_LineHeight_Normal", @"<div style='width:400px;line-height:normal;'>
                <span>Single line of text</span>
              </div>"),
        ("S10_8_1_LineHeight_Number", @"<div style='width:400px;font-size:16px;line-height:2;'>
                <span>Text with 2x line-height</span>
              </div>"),
        ("S10_8_1_LineHeight_Length", @"<div style='width:400px;line-height:50px;'>
                <span>Text with 50px line-height</span>
              </div>"),
        ("S10_8_1_LineHeight_Percentage_htmlNormal", @"<div style='width:400px;font-size:20px;line-height:normal;'>
                <span>Text</span>
              </div>"),
        ("S10_8_1_LineHeight_Percentage_htmlDouble", @"<div style='width:400px;font-size:20px;line-height:200%;'>
                <span>Text with 200% line-height</span>
              </div>"),
        ("S10_8_1_Leading_IncreasesLineBox_htmlSmall", @"<div style='width:400px;font-size:14px;line-height:14px;'>
                <span>Tight</span>
              </div>"),
        ("S10_8_1_Leading_IncreasesLineBox_htmlLarge", @"<div style='width:400px;font-size:14px;line-height:40px;'>
                <span>Loose</span>
              </div>"),
        ("S10_8_1_InlineBoxHeight", @"<div style='width:400px;font-size:16px;line-height:30px;'>
                <span style='background-color:yellow;'>Inline content</span>
              </div>"),
        ("S10_8_1_Strut_EmptyLineHasHeight", @"<div style='width:400px;line-height:30px;'>&nbsp;</div>"),
        ("S10_8_1_Golden_LineHeightVariations", @"<div style='width:400px;'>
                <div style='line-height:20px;background-color:red;'>Line 1</div>
                <div style='line-height:40px;background-color:blue;'>Line 2</div>
              </div>"),
        ("S10_8_2_VerticalAlign_Baseline", @"<div style='width:400px;font-size:16px;'>
                <span style='vertical-align:baseline;background-color:yellow;'>Baseline</span>
                <span style='background-color:lime;'>Reference</span>
              </div>"),
        ("S10_8_2_VerticalAlign_Middle", @"<div style='width:400px;font-size:16px;'>
                <span style='vertical-align:middle;background-color:yellow;'>Middle</span>
                <span style='background-color:lime;'>Reference</span>
              </div>"),
        ("S10_8_2_VerticalAlign_Sub", @"<div style='width:400px;font-size:16px;'>
                <span>Normal</span>
                <span style='vertical-align:sub;background-color:yellow;'>Sub</span>
              </div>"),
        ("S10_8_2_VerticalAlign_Super", @"<div style='width:400px;font-size:16px;'>
                <span>Normal</span>
                <span style='vertical-align:super;background-color:yellow;'>Super</span>
              </div>"),
        ("S10_8_2_VerticalAlign_TextTop", @"<div style='width:400px;font-size:16px;'>
                <span style='vertical-align:text-top;font-size:24px;background-color:yellow;'>TextTop</span>
                <span style='background-color:lime;'>Ref</span>
              </div>"),
        ("S10_8_2_VerticalAlign_TextBottom", @"<div style='width:400px;font-size:16px;'>
                <span style='vertical-align:text-bottom;font-size:24px;background-color:yellow;'>TextBottom</span>
                <span style='background-color:lime;'>Ref</span>
              </div>"),
        ("S10_8_2_VerticalAlign_Top", @"<div style='width:400px;font-size:16px;line-height:40px;'>
                <span style='vertical-align:top;background-color:yellow;'>Top</span>
                <span style='background-color:lime;'>Reference</span>
              </div>"),
        ("S10_8_2_VerticalAlign_Bottom", @"<div style='width:400px;font-size:16px;line-height:40px;'>
                <span style='vertical-align:bottom;background-color:yellow;'>Bottom</span>
                <span style='background-color:lime;'>Reference</span>
              </div>"),
        ("S10_8_2_VerticalAlign_Percentage", @"<div style='width:400px;font-size:16px;line-height:20px;'>
                <span style='vertical-align:50%;background-color:yellow;'>50%</span>
                <span style='background-color:lime;'>Reference</span>
              </div>"),
        ("S10_8_2_VerticalAlign_Length", @"<div style='width:400px;font-size:16px;'>
                <span style='vertical-align:10px;background-color:yellow;'>+10px</span>
                <span style='background-color:lime;'>Reference</span>
              </div>"),
        ("S10_8_2_VerticalAlign_AppliesOnlyToInline", @"<div style='width:400px;'>
                <div style='vertical-align:middle;height:50px;background-color:red;'>Block</div>
              </div>"),
        ("S10_8_2_VerticalAlign_TableCell", @"<table style='width:300px;height:100px;border-collapse:collapse;'>
                <tr>
                    <td style='vertical-align:middle;background-color:yellow;'>Middle</td>
                    <td style='vertical-align:top;background-color:lime;'>Top</td>
                    <td style='vertical-align:bottom;background-color:cyan;'>Bottom</td>
                </tr>
              </table>"),
        ("S10_8_2_VerticalAlign_InlineBlock_Baseline", @"<div style='width:400px;font-size:16px;'>
                <span style='display:inline-block;width:50px;height:40px;
                             vertical-align:baseline;background-color:red;'></span>
                <span style='background-color:lime;'>Ref</span>
              </div>"),
        ("S10_8_2_VerticalAlign_InlineBlock_Top", @"<div style='width:400px;font-size:16px;line-height:50px;'>
                <span style='display:inline-block;width:50px;height:30px;
                             vertical-align:top;background-color:blue;'></span>
                <span style='background-color:lime;'>Ref</span>
              </div>"),
        ("S10_8_2_VerticalAlign_NegativeLength", @"<div style='width:400px;font-size:16px;'>
                <span style='vertical-align:-5px;background-color:yellow;'>-5px</span>
                <span style='background-color:lime;'>Reference</span>
              </div>"),
        ("S10_8_2_VerticalAlign_MixedAlignments", @"<div style='width:400px;font-size:14px;line-height:40px;'>
                <span style='vertical-align:top;background-color:red;'>Top</span>
                <span style='vertical-align:middle;background-color:green;'>Mid</span>
                <span style='vertical-align:bottom;background-color:blue;color:white;'>Bot</span>
              </div>"),
        ("S10_8_2_VerticalAlign_Super_DifferentFontSizes", @"<div style='width:400px;font-size:20px;'>
                <span>Normal</span>
                <span style='vertical-align:super;font-size:12px;background-color:yellow;'>Super small</span>
              </div>"),
        ("S10_8_2_Golden_VerticalAlignVariations", @"<div style='width:400px;font-size:16px;line-height:40px;'>
                <span style='vertical-align:top;background-color:red;'>Top</span>
                <span style='vertical-align:bottom;background-color:blue;color:white;'>Bottom</span>
                <span style='background-color:green;'>Normal</span>
              </div>"),
        ("Pixel_S10_8_VerticalAlign_Positioning", @"<body style='margin:0;padding:0;'>
                <div style='width:400px;line-height:60px;font-size:14px;'>
                    <span style='vertical-align:top;background-color:red;'>Top</span>
                    <span style='vertical-align:bottom;background-color:blue;color:white;'>Bot</span>
                </div>
              </body>"),
    ];

    /// <summary>CSS 2.1 Chapter 17 test snippets (95 tests).</summary>
    internal static readonly (string Name, string Html)[] Chapter17 =
    [
        ("S17_1_HtmlTableStructure", @"<table style='width:300px;border:1px solid black;'>
                <tr><td>Cell 1</td><td>Cell 2</td></tr>
                <tr><td>Cell 3</td><td>Cell 4</td></tr>
              </table>"),
        ("S17_1_TableWithAllComponents", @"<table style='width:400px;border:1px solid black;'>
                <caption>My Table</caption>
                <thead><tr><th>H1</th><th>H2</th></tr></thead>
                <tbody><tr><td>A</td><td>B</td></tr></tbody>
                <tfoot><tr><td>F1</td><td>F2</td></tr></tfoot>
              </table>"),
        ("S17_1_AnyElementAsTableComponent", @"<div style='display:table;width:300px;border:1px solid black;'>
                <div style='display:table-row;'>
                  <div style='display:table-cell;padding:5px;'>Cell A</div>
                  <div style='display:table-cell;padding:5px;'>Cell B</div>
                </div>
              </div>"),
        ("S17_2_DisplayTable_BlockLevel", @"<div style='width:500px;'>
                <div style='display:table;border:1px solid black;'>
                  <div style='display:table-row;'>
                    <div style='display:table-cell;'>Cell</div>
                  </div>
                </div>
              </div>"),
        ("S17_2_DisplayInlineTable", @"<div style='width:500px;'>
                Before
                <div style='display:inline-table;border:1px solid black;'>
                  <div style='display:table-row;'>
                    <div style='display:table-cell;'>Cell</div>
                  </div>
                </div>
                After
              </div>"),
        ("S17_2_DisplayTableRow", @"<div style='display:table;width:300px;'>
                <div style='display:table-row;'>
                  <div style='display:table-cell;background:red;'>A</div>
                  <div style='display:table-cell;background:blue;color:white;'>B</div>
                </div>
              </div>"),
        ("S17_2_DisplayTableRowGroup", @"<div style='display:table;width:300px;'>
                <div style='display:table-row-group;'>
                  <div style='display:table-row;'>
                    <div style='display:table-cell;'>R1C1</div>
                  </div>
                  <div style='display:table-row;'>
                    <div style='display:table-cell;'>R2C1</div>
                  </div>
                </div>
              </div>"),
        ("S17_2_DisplayTableHeaderGroup", @"<table style='width:300px;'>
                <thead><tr><th>Header</th></tr></thead>
                <tbody><tr><td>Body</td></tr></tbody>
              </table>"),
        ("S17_2_DisplayTableFooterGroup", @"<table style='width:300px;'>
                <tbody><tr><td>Body</td></tr></tbody>
                <tfoot><tr><td>Footer</td></tr></tfoot>
              </table>"),
        ("S17_2_DisplayTableColumn", @"<table style='width:300px;'>
                <col style='width:100px;'/>
                <col style='width:200px;'/>
                <tr><td>A</td><td>B</td></tr>
              </table>"),
        ("S17_2_DisplayTableColumnGroup", @"<table style='width:300px;'>
                <colgroup><col style='width:150px;'/><col/></colgroup>
                <tr><td>A</td><td>B</td></tr>
              </table>"),
        ("S17_2_DisplayTableCell", @"<div style='display:table;width:300px;'>
                <div style='display:table-row;'>
                  <div style='display:table-cell;border:1px solid black;padding:5px;'>Cell</div>
                </div>
              </div>"),
        ("S17_2_DisplayTableCaption", @"<table style='width:300px;border:1px solid black;'>
                <caption>Table Caption</caption>
                <tr><td>A</td><td>B</td></tr>
              </table>"),
        ("S17_2_1_AnonymousTableWrapper_ForOrphanRow", @"<div style='width:300px;'>
                <div style='display:table-row;'>
                  <div style='display:table-cell;'>Orphan cell</div>
                </div>
              </div>"),
        ("S17_2_1_AnonymousRowWrapper_ForOrphanCell", @"<div style='display:table;width:300px;'>
                <div style='display:table-cell;'>Cell without explicit row</div>
              </div>"),
        ("S17_2_1_MissingColumnWrapper", @"<table style='width:300px;'>
                <col style='width:100px;'/>
                <tr><td>A</td><td>B</td></tr>
              </table>"),
        ("S17_2_1_MissingTableElement", @"<div style='width:400px;'>
                <div style='display:table-cell;'>Cell outside any table</div>
              </div>"),
        ("S17_2_1_Golden_AnonymousTableInheritance", @"<div style='width:300px;color:red;'>
                <div style='display:table-row;'>
                  <div style='display:table-cell;'>Inherited color</div>
                </div>
              </div>"),
        ("S17_2_1_AnonymousTableMultipleRows", @"<div style='width:400px;'>
                <div style='display:table-row;'>
                  <div style='display:table-cell;'>Row 1</div>
                </div>
                <div style='display:table-row;'>
                  <div style='display:table-cell;'>Row 2</div>
                </div>
              </div>"),
        ("S17_3_ColumnsDoNotGenerateBoxes", @"<table style='width:300px;'>
                <col style='width:100px;'/><col style='width:200px;'/>
                <tr><td>A</td><td>B</td></tr>
              </table>"),
        ("S17_3_ColumnBackground", @"<table style='width:200px;border-collapse:collapse;'>
                <col style='background-color:red;'/>
                <col style='background-color:blue;'/>
                <tr><td style='padding:10px;'>&nbsp;</td><td style='padding:10px;'>&nbsp;</td></tr>
              </table>"),
        ("S17_3_ColumnWidthSetsMinimum", @"<table style='width:400px;'>
                <col style='width:200px;'/><col/>
                <tr><td>Wide col</td><td>Auto col</td></tr>
              </table>"),
        ("S17_3_ColumnBorderCollapsed", @"<table style='width:300px;border-collapse:collapse;'>
                <col style='border:2px solid red;'/><col/>
                <tr><td>A</td><td>B</td></tr>
              </table>"),
        ("S17_3_ColumnVisibilityCollapse", @"<table style='width:300px;'>
                <col style='visibility:collapse;'/><col/>
                <tr><td>Hidden</td><td>Visible</td></tr>
              </table>"),
        ("S17_4_TableWrapperBox", @"<table style='width:300px;border:1px solid black;'>
                <caption>My Caption</caption>
                <tr><td>A</td><td>B</td></tr>
              </table>"),
        ("S17_4_TableGeneratesBFC", @"<table style='width:400px;border:1px solid black;'>
                <tr><td>
                  <div style='float:left;width:50px;height:50px;background:red;'></div>
                  <div>Text next to float</div>
                </td></tr>
              </table>"),
        ("S17_4_TableWidthAuto", @"<table style='border:1px solid black;'>
                <tr><td>Short</td><td>Longer cell content here</td></tr>
              </table>"),
        ("S17_4_TableMarginsAndPadding", @"<div style='width:500px;'>
                <table style='width:300px;margin:20px;border:2px solid black;padding:10px;'>
                  <tr><td>Cell</td></tr>
                </table>
              </div>"),
        ("S17_4_TableSpecifiedWidth", @"<table style='width:350px;border:1px solid black;'>
                <tr><td>A</td><td>B</td><td>C</td></tr>
              </table>"),
        ("S17_4_1_CaptionSideTop", @"<table style='width:300px;border:1px solid black;caption-side:top;'>
                <caption style='background:yellow;'>Top Caption</caption>
                <tr><td>A</td><td>B</td></tr>
              </table>"),
        ("S17_4_1_CaptionSideBottom", @"<table style='width:300px;border:1px solid black;caption-side:bottom;'>
                <caption style='background:yellow;'>Bottom Caption</caption>
                <tr><td>A</td><td>B</td></tr>
              </table>"),
        ("S17_4_1_CaptionBlockLevel", @"<table style='width:300px;border:2px solid black;'>
                <caption style='padding:5px;background:lightblue;'>Caption Block</caption>
                <tr><td>Cell</td></tr>
              </table>"),
        ("S17_4_1_CaptionWidth", @"<table style='width:400px;border:1px solid black;'>
                <caption style='background:yellow;'>Caption should span table width</caption>
                <tr><td>A</td><td>B</td></tr>
              </table>"),
        ("S17_4_1_Golden_CaptionMarginCollapsing", @"<div style='width:400px;'>
                <table style='width:300px;border:1px solid black;margin-top:10px;'>
                  <caption style='margin-bottom:15px;background:yellow;'>Caption</caption>
                  <tr><td>A</td><td>B</td></tr>
                </table>
              </div>"),
        ("S17_5_1_Layer1_TableBackground", @"<table style='width:200px;background-color:red;border-collapse:collapse;'>
                <tr><td style='padding:10px;'>&nbsp;</td></tr>
              </table>"),
        ("S17_5_1_Layer5_RowBackground", @"<table style='width:200px;background-color:red;border-collapse:collapse;'>
                <tr style='background-color:blue;'><td style='padding:10px;'>&nbsp;</td></tr>
              </table>"),
        ("S17_5_1_Layer6_CellBackground", @"<table style='width:200px;background-color:red;border-collapse:collapse;'>
                <tr style='background-color:blue;'>
                  <td style='background-color:lime;padding:10px;'>&nbsp;</td>
                </tr>
              </table>"),
        ("S17_5_1_TransparentCellShowsRow", @"<table style='width:200px;border-collapse:collapse;'>
                <tr style='background-color:blue;'>
                  <td style='padding:10px;'>&nbsp;</td>
                </tr>
              </table>"),
        ("S17_5_1_Golden_RowGroupBackground", @"<table style='width:300px;border-collapse:collapse;'>
                <tbody style='background-color:yellow;'>
                  <tr><td style='padding:8px;'>A</td><td style='padding:8px;'>B</td></tr>
                </tbody>
              </table>"),
        ("S17_5_1_ColumnGroupBackground", @"<table style='width:300px;border-collapse:collapse;'>
                <colgroup style='background-color:lightgray;'>
                  <col style='background-color:lightyellow;'/>
                  <col/>
                </colgroup>
                <tr><td style='padding:8px;'>A</td><td style='padding:8px;'>B</td></tr>
              </table>"),
        ("S17_5_1_MultipleLayers", @"<table style='width:300px;background:lightgray;border-collapse:collapse;'>
                <tbody style='background:lightyellow;'>
                  <tr style='background:lightblue;'>
                    <td style='background:lightgreen;padding:8px;'>Cell</td>
                    <td style='padding:8px;'>Transparent</td>
                  </tr>
                </tbody>
              </table>"),
        ("S17_5_2_1_FixedLayout_FirstRowDeterminesWidths", @"<table style='width:400px;table-layout:fixed;border-collapse:collapse;'>
                <tr><td style='width:100px;'>Narrow</td><td>Auto</td></tr>
                <tr><td>Row 2 A</td><td>Row 2 B</td></tr>
              </table>"),
        ("S17_5_2_1_FixedLayout_ColumnElements", @"<table style='width:400px;table-layout:fixed;border-collapse:collapse;'>
                <col style='width:150px;'/><col style='width:250px;'/>
                <tr><td>A</td><td>B</td></tr>
              </table>"),
        ("S17_5_2_1_FixedLayout_EqualDistribution", @"<table style='width:400px;table-layout:fixed;border-collapse:collapse;'>
                <tr><td>A</td><td>B</td><td>C</td><td>D</td></tr>
              </table>"),
        ("S17_5_2_1_FixedLayout_TableWidthForcesWider", @"<table style='width:600px;table-layout:fixed;border-collapse:collapse;'>
                <col style='width:100px;'/><col style='width:100px;'/>
                <tr><td>A</td><td>B</td></tr>
              </table>"),
        ("S17_5_2_1_Golden_FixedLayout", @"<table style='width:400px;table-layout:fixed;border-collapse:collapse;'>
                <col style='width:200px;'/><col/>
                <tr><td style='padding:5px;'>Fixed col</td><td style='padding:5px;'>Auto col</td></tr>
                <tr><td style='padding:5px;'>R2C1</td><td style='padding:5px;'>R2C2</td></tr>
              </table>"),
        ("S17_5_2_2_AutoLayout_ContentDeterminesWidths", @"<table style='border-collapse:collapse;'>
                <tr>
                  <td style='padding:5px;'>Short</td>
                  <td style='padding:5px;'>A much longer cell content that needs more space</td>
                </tr>
              </table>"),
        ("S17_5_2_2_AutoLayout_CellWidthMinimum", @"<table style='border-collapse:collapse;'>
                <tr>
                  <td style='width:200px;padding:5px;'>Min 200px</td>
                  <td style='padding:5px;'>Auto</td>
                </tr>
              </table>"),
        ("S17_5_2_2_AutoLayout_SpanningCells", @"<table style='width:400px;border-collapse:collapse;'>
                <tr><td>A</td><td>B</td><td>C</td></tr>
                <tr><td colspan='2'>Spanning A+B</td><td>C2</td></tr>
              </table>"),
        ("S17_5_2_2_AutoLayout_TableWidthConstraint", @"<table style='width:300px;border-collapse:collapse;'>
                <tr>
                  <td style='padding:5px;'>Col1</td>
                  <td style='padding:5px;'>Col2</td>
                  <td style='padding:5px;'>Col3</td>
                </tr>
              </table>"),
        ("S17_5_2_2_Golden_AutoLayout", @"<table style='width:400px;border-collapse:collapse;'>
                <tr>
                  <td style='width:100px;padding:5px;'>Fixed</td>
                  <td style='padding:5px;'>Auto content</td>
                </tr>
              </table>"),
        ("S17_5_2_2_AutoLayout_MinMaxContentWidths", @"<table style='border-collapse:collapse;'>
                <tr>
                  <td style='padding:5px;'>A</td>
                  <td style='padding:5px;'>Medium text</td>
                  <td style='padding:5px;'>This is a longer piece of text content</td>
                </tr>
              </table>"),
        ("S17_5_2_2_AutoLayout_ColumnMinWidth", @"<table style='border-collapse:collapse;'>
                <tr>
                  <td style='padding:5px;'>X</td>
                  <td style='padding:5px;'>Y</td>
                </tr>
                <tr>
                  <td style='padding:5px;'>LongerContentInCol1</td>
                  <td style='padding:5px;'>Z</td>
                </tr>
              </table>"),
        ("S17_5_3_RowHeight_MaxOfCellHeights", @"<table style='width:300px;border-collapse:collapse;'>
                <tr>
                  <td style='height:50px;background:red;'>Tall</td>
                  <td style='height:30px;background:blue;'>Short</td>
                </tr>
              </table>"),
        ("S17_5_3_MinimumRowHeight", @"<table style='width:300px;border-collapse:collapse;'>
                <tr style='height:80px;'>
                  <td style='background:lightblue;'>Cell</td>
                </tr>
              </table>"),
        ("S17_5_3_PercentageHeight", @"<table style='width:300px;height:200px;border-collapse:collapse;'>
                <tr style='height:50%;'>
                  <td style='background:lightgreen;'>50% height</td>
                </tr>
                <tr>
                  <td style='background:lightyellow;'>Rest</td>
                </tr>
              </table>"),
        ("S17_5_3_ExtraHeightDistributed", @"<table style='width:300px;height:300px;border-collapse:collapse;'>
                <tr><td style='background:red;'>Row 1</td></tr>
                <tr><td style='background:blue;color:white;'>Row 2</td></tr>
              </table>"),
        ("S17_5_3_Golden_TableHeight", @"<table style='width:300px;height:200px;border-collapse:collapse;'>
                <tr><td style='height:40px;padding:5px;'>Fixed height</td></tr>
                <tr><td style='padding:5px;'>Auto height</td></tr>
              </table>"),
        ("S17_5_4_TextAlignInCells", @"<table style='width:300px;border-collapse:collapse;'>
                <tr>
                  <td style='text-align:left;padding:5px;'>Left</td>
                  <td style='text-align:center;padding:5px;'>Center</td>
                  <td style='text-align:right;padding:5px;'>Right</td>
                </tr>
              </table>"),
        ("S17_5_4_ColumnAlignmentInheritance", @"<table style='width:300px;border-collapse:collapse;'>
                <tr>
                  <td style='text-align:center;padding:5px;'>Centered</td>
                  <td style='padding:5px;'>Default</td>
                </tr>
              </table>"),
        ("S17_5_5_RowVisibilityCollapse", @"<table style='width:300px;border-collapse:collapse;'>
                <tr><td style='padding:5px;'>Visible Row 1</td></tr>
                <tr style='visibility:collapse;'><td style='padding:5px;'>Hidden Row</td></tr>
                <tr><td style='padding:5px;'>Visible Row 3</td></tr>
              </table>"),
        ("S17_5_5_ColumnVisibilityCollapse", @"<table style='width:300px;border-collapse:collapse;'>
                <col/><col style='visibility:collapse;'/><col/>
                <tr><td>A</td><td>B</td><td>C</td></tr>
              </table>"),
        ("S17_5_5_CollapseKeepsDimensions", @"<table style='width:300px;border-collapse:collapse;'>
                <tr><td style='height:40px;padding:5px;'>Row 1</td></tr>
                <tr style='visibility:collapse;'><td style='height:40px;padding:5px;'>Collapsed</td></tr>
              </table>"),
        ("S17_6_1_SeparateBorders", @"<table style='width:300px;border-collapse:separate;border:2px solid black;'>
                <tr>
                  <td style='border:1px solid red;padding:5px;'>A</td>
                  <td style='border:1px solid blue;padding:5px;'>B</td>
                </tr>
              </table>"),
        ("S17_6_1_BorderSpacing_OneValue", @"<table style='width:300px;border-collapse:separate;border-spacing:10px;border:1px solid black;'>
                <tr>
                  <td style='border:1px solid red;padding:5px;'>A</td>
                  <td style='border:1px solid blue;padding:5px;'>B</td>
                </tr>
              </table>"),
        ("S17_6_1_BorderSpacing_TwoValues", @"<table style='width:300px;border-collapse:separate;border-spacing:15px 5px;border:1px solid black;'>
                <tr>
                  <td style='border:1px solid red;padding:5px;'>A</td>
                  <td style='border:1px solid blue;padding:5px;'>B</td>
                </tr>
                <tr>
                  <td style='border:1px solid green;padding:5px;'>C</td>
                  <td style='border:1px solid orange;padding:5px;'>D</td>
                </tr>
              </table>"),
        ("S17_6_1_BorderSpacingTableOnly", @"<div style='border-spacing:20px;width:300px;'>
                <table style='width:300px;border-collapse:separate;border-spacing:10px;'>
                  <tr><td style='border:1px solid black;padding:5px;'>Cell</td></tr>
                </table>
              </div>"),
        ("S17_6_1_SpacingOutermostCells", @"<table style='width:300px;border-collapse:separate;border-spacing:15px;border:2px solid black;'>
                <tr>
                  <td style='border:1px solid red;padding:5px;'>A</td>
                  <td style='border:1px solid blue;padding:5px;'>B</td>
                </tr>
              </table>"),
        ("S17_6_1_Pixel_SeparateBorders", @"<table style='width:200px;border-collapse:separate;border:3px solid red;border-spacing:10px;'>
                <tr><td style='border:2px solid blue;padding:10px;background:white;'>Cell</td></tr>
              </table>"),
        ("S17_6_1_Golden_SeparateBorders", @"<table style='width:300px;border-collapse:separate;border-spacing:8px;border:2px solid black;'>
                <tr>
                  <td style='border:1px solid gray;padding:5px;'>A</td>
                  <td style='border:1px solid gray;padding:5px;'>B</td>
                </tr>
                <tr>
                  <td style='border:1px solid gray;padding:5px;'>C</td>
                  <td style='border:1px solid gray;padding:5px;'>D</td>
                </tr>
              </table>"),
        ("S17_6_1_1_EmptyCellsShow", @"<table style='width:300px;border-collapse:separate;empty-cells:show;'>
                <tr>
                  <td style='border:1px solid black;background:red;padding:5px;'>Full</td>
                  <td style='border:1px solid black;background:blue;padding:5px;'></td>
                </tr>
              </table>"),
        ("S17_6_1_1_EmptyCellsHide", @"<table style='width:300px;border-collapse:separate;empty-cells:hide;'>
                <tr>
                  <td style='border:1px solid black;background:red;padding:5px;'>Full</td>
                  <td style='border:1px solid black;background:blue;padding:5px;'></td>
                </tr>
              </table>"),
        ("S17_6_1_1_WhitespaceOnlyEmpty", @"<table style='width:300px;border-collapse:separate;empty-cells:hide;'>
                <tr>
                  <td style='border:1px solid black;background:red;padding:5px;'>Full</td>
                  <td style='border:1px solid black;background:blue;padding:5px;'>   </td>
                </tr>
              </table>"),
        ("S17_6_1_1_AllHiddenEmptyRow", @"<table style='width:300px;border-collapse:separate;empty-cells:hide;border-spacing:5px;'>
                <tr>
                  <td style='border:1px solid black;background:red;padding:5px;'>Full</td>
                </tr>
                <tr>
                  <td style='border:1px solid black;background:blue;padding:5px;'></td>
                </tr>
              </table>"),
        ("S17_6_2_CollapsingBorders", @"<table style='width:300px;border-collapse:collapse;'>
                <tr>
                  <td style='border:2px solid black;padding:5px;'>A</td>
                  <td style='border:2px solid black;padding:5px;'>B</td>
                </tr>
                <tr>
                  <td style='border:2px solid black;padding:5px;'>C</td>
                  <td style='border:2px solid black;padding:5px;'>D</td>
                </tr>
              </table>"),
        ("S17_6_2_CollapsingBorderSpacingZero", @"<table style='width:300px;border-collapse:collapse;border-spacing:10px;'>
                <tr>
                  <td style='border:1px solid black;padding:5px;'>A</td>
                  <td style='border:1px solid black;padding:5px;'>B</td>
                </tr>
              </table>"),
        ("S17_6_2_PaddingStillApplies", @"<table style='width:300px;border-collapse:collapse;'>
                <tr>
                  <td style='border:1px solid black;padding:20px;background:lightblue;'>Padded</td>
                </tr>
              </table>"),
        ("S17_6_2_BordersExtendIntoMargin", @"<div style='width:400px;'>
                <table style='width:300px;border-collapse:collapse;margin:10px;'>
                  <tr>
                    <td style='border:4px solid red;padding:5px;'>Cell</td>
                  </tr>
                </table>
              </div>"),
        ("S17_6_2_OddPixelBorders", @"<table style='width:300px;border-collapse:collapse;'>
                <tr>
                  <td style='border:3px solid black;padding:5px;'>A</td>
                  <td style='border:3px solid black;padding:5px;'>B</td>
                </tr>
              </table>"),
        ("S17_6_2_Golden_CollapsingBorders", @"<table style='width:300px;border-collapse:collapse;border:2px solid black;'>
                <tr>
                  <td style='border:1px solid gray;padding:5px;'>A</td>
                  <td style='border:1px solid gray;padding:5px;'>B</td>
                </tr>
                <tr>
                  <td style='border:1px solid gray;padding:5px;'>C</td>
                  <td style='border:1px solid gray;padding:5px;'>D</td>
                </tr>
              </table>"),
        ("S17_6_2_1_WiderBorderWins", @"<table style='width:300px;border-collapse:collapse;border:1px solid red;'>
                <tr>
                  <td style='border:5px solid blue;padding:5px;'>Wide cell border</td>
                  <td style='border:1px solid red;padding:5px;'>Thin cell border</td>
                </tr>
              </table>"),
        ("S17_6_2_1_HiddenWins", @"<table style='width:300px;border-collapse:collapse;border:2px solid black;'>
                <tr>
                  <td style='border-right:hidden;padding:5px;'>Hidden right</td>
                  <td style='border:2px solid red;padding:5px;'>Visible</td>
                </tr>
              </table>"),
        ("S17_6_2_1_BorderStylePriority", @"<table style='width:300px;border-collapse:collapse;'>
                <tr>
                  <td style='border:3px solid red;padding:5px;'>Solid</td>
                  <td style='border:3px dashed blue;padding:5px;'>Dashed</td>
                </tr>
              </table>"),
        ("S17_6_2_1_CellWinsOverRow", @"<table style='width:300px;border-collapse:collapse;border:2px solid green;'>
                <tr style='border:2px solid blue;'>
                  <td style='border:2px solid red;padding:5px;'>Cell wins</td>
                </tr>
              </table>"),
        ("S17_6_2_1_LeftAndTopWins", @"<table style='width:300px;border-collapse:collapse;'>
                <tr>
                  <td style='border:2px solid red;padding:5px;'>Left cell</td>
                  <td style='border:2px solid blue;padding:5px;'>Right cell</td>
                </tr>
              </table>"),
        ("S17_6_2_1_Golden_BorderConflicts", @"<table style='width:300px;border-collapse:collapse;border:3px solid green;'>
                <tr style='border:2px solid blue;'>
                  <td style='border:5px solid red;padding:5px;'>Cell</td>
                  <td style='border:1px solid gray;padding:5px;'>Cell</td>
                </tr>
              </table>"),
        ("S17_6_3_AllBorderStyles", @"<table style='width:400px;border-collapse:separate;border-spacing:5px;'>
                <tr>
                  <td style='border:3px solid black;padding:5px;'>solid</td>
                  <td style='border:3px dashed black;padding:5px;'>dashed</td>
                  <td style='border:3px dotted black;padding:5px;'>dotted</td>
                </tr>
                <tr>
                  <td style='border:3px double black;padding:5px;'>double</td>
                  <td style='border:3px groove gray;padding:5px;'>groove</td>
                  <td style='border:3px ridge gray;padding:5px;'>ridge</td>
                </tr>
                <tr>
                  <td style='border:3px inset gray;padding:5px;'>inset</td>
                  <td style='border:3px outset gray;padding:5px;'>outset</td>
                  <td style='border:3px none;padding:5px;'>none</td>
                </tr>
              </table>"),
        ("S17_6_3_InsetOnTable", @"<table style='width:300px;border:4px inset gray;border-collapse:separate;'>
                <tr><td style='padding:10px;'>Inset table</td></tr>
              </table>"),
        ("S17_6_3_OutsetOnTable", @"<table style='width:300px;border:4px outset gray;border-collapse:separate;'>
                <tr><td style='padding:10px;'>Outset table</td></tr>
              </table>"),
        ("S17_6_3_CollapsingModelBorderStyleMapping", @"<table style='width:300px;border-collapse:collapse;border:3px groove gray;'>
                <tr>
                  <td style='border:3px ridge gray;padding:5px;'>Ridge</td>
                  <td style='border:3px inset gray;padding:5px;'>Inset</td>
                </tr>
                <tr>
                  <td style='border:3px outset gray;padding:5px;'>Outset</td>
                  <td style='border:3px groove gray;padding:5px;'>Groove</td>
                </tr>
              </table>"),
        ("S17_Integration_RowspanColspan", @"<table style='width:400px;border-collapse:collapse;border:1px solid black;'>
                <tr>
                  <td rowspan='2' style='border:1px solid black;padding:5px;'>R1-2</td>
                  <td colspan='2' style='border:1px solid black;padding:5px;'>C1-2</td>
                </tr>
                <tr>
                  <td style='border:1px solid black;padding:5px;'>B</td>
                  <td style='border:1px solid black;padding:5px;'>C</td>
                </tr>
              </table>"),
        ("S17_Integration_NestedTables", @"<table style='width:400px;border:1px solid black;'>
                <tr>
                  <td style='padding:5px;'>
                    <table style='width:100%;border:1px solid red;'>
                      <tr><td>Nested</td></tr>
                    </table>
                  </td>
                  <td style='padding:5px;'>Outer</td>
                </tr>
              </table>"),
        ("S17_Integration_MixedHtmlCssTable", @"<div style='display:table;width:300px;border:1px solid black;'>
                <div style='display:table-caption;background:yellow;padding:5px;'>CSS Caption</div>
                <div style='display:table-header-group;background:lightblue;'>
                  <div style='display:table-row;'>
                    <div style='display:table-cell;padding:5px;border:1px solid gray;'>H1</div>
                    <div style='display:table-cell;padding:5px;border:1px solid gray;'>H2</div>
                  </div>
                </div>
                <div style='display:table-row-group;'>
                  <div style='display:table-row;'>
                    <div style='display:table-cell;padding:5px;border:1px solid gray;'>A</div>
                    <div style='display:table-cell;padding:5px;border:1px solid gray;'>B</div>
                  </div>
                </div>
                <div style='display:table-footer-group;background:lightyellow;'>
                  <div style='display:table-row;'>
                    <div style='display:table-cell;padding:5px;border:1px solid gray;'>F1</div>
                    <div style='display:table-cell;padding:5px;border:1px solid gray;'>F2</div>
                  </div>
                </div>
              </div>"),
        ("S17_Integration_Pixel_MultiLayerBackgrounds", @"<table style='width:300px;background:red;border-collapse:collapse;'>
                <tr style='background:blue;'>
                  <td style='background:lime;padding:15px;'>&nbsp;</td>
                  <td style='padding:15px;'>&nbsp;</td>
                </tr>
              </table>"),
        ("S17_Integration_Golden_ComplexTable", @"<table style='width:400px;border-collapse:collapse;border:2px solid black;'>
                <caption style='padding:5px;background:lightyellow;'>Complex Table</caption>
                <thead>
                  <tr style='background:lightblue;'>
                    <th style='border:1px solid gray;padding:5px;'>Col A</th>
                    <th style='border:1px solid gray;padding:5px;'>Col B</th>
                    <th style='border:1px solid gray;padding:5px;'>Col C</th>
                  </tr>
                </thead>
                <tbody>
                  <tr>
                    <td style='border:1px solid gray;padding:5px;'>1</td>
                    <td style='border:1px solid gray;padding:5px;'>2</td>
                    <td style='border:1px solid gray;padding:5px;'>3</td>
                  </tr>
                  <tr>
                    <td colspan='2' style='border:1px solid gray;padding:5px;'>Span</td>
                    <td style='border:1px solid gray;padding:5px;'>4</td>
                  </tr>
                </tbody>
              </table>"),
    ];

    /// <summary>CSS 2.1 Chapter 12 test snippets (20 tests).</summary>
    internal static readonly (string Name, string Html)[] Chapter12 =
    [
        ("S12_1_ListStyleType_Disc", @"<ul style='list-style-type:disc;'><li>Item A</li><li>Item B</li></ul>"),
        ("S12_2_ListStyleType_Circle", @"<ul style='list-style-type:circle;'><li>Item A</li><li>Item B</li></ul>"),
        ("S12_3_ListStyleType_Square", @"<ul style='list-style-type:square;'><li>Item A</li><li>Item B</li></ul>"),
        ("S12_4_ListStyleType_Decimal", @"<ol style='list-style-type:decimal;'><li>First</li><li>Second</li><li>Third</li></ol>"),
        ("S12_5_ListStyleType_LowerRoman", @"<ol style='list-style-type:lower-roman;'><li>First</li><li>Second</li><li>Third</li></ol>"),
        ("S12_6_ListStyleType_UpperRoman", @"<ol style='list-style-type:upper-roman;'><li>First</li><li>Second</li><li>Third</li></ol>"),
        ("S12_7_ListStyleType_LowerAlpha", @"<ol style='list-style-type:lower-alpha;'><li>First</li><li>Second</li><li>Third</li></ol>"),
        ("S12_8_ListStyleType_UpperAlpha", @"<ol style='list-style-type:upper-alpha;'><li>First</li><li>Second</li><li>Third</li></ol>"),
        ("S12_9_ListStyleType_None", @"<ul style='list-style-type:none;'><li>No marker A</li><li>No marker B</li></ul>"),
        ("S12_10_ListStylePosition_Inside", @"<ul style='list-style-position:inside;'><li>Inside marker</li><li>Second item</li></ul>"),
        ("S12_11_ListStylePosition_Outside", @"<ul style='list-style-position:outside;'><li>Outside marker</li><li>Second item</li></ul>"),
        ("S12_12_ListStyle_Shorthand_SquareInside", @"<ul style='list-style:square inside;'><li>Shorthand A</li><li>Shorthand B</li></ul>"),
        ("S12_13_ListStyle_Shorthand_DecimalOutside", @"<ol style='list-style:decimal outside;'><li>One</li><li>Two</li></ol>"),
        ("S12_14_OrderedList_StartAttribute", @"<ol start='5' style='list-style-type:decimal;'><li>Five</li><li>Six</li><li>Seven</li></ol>"),
        ("S12_15_NestedLists_UlInUl", @"<ul style='list-style-type:disc;'><li>Outer A<ul style='list-style-type:circle;'><li>Inner 1</li><li>Inner 2</li></ul></li><li>Outer B</li></ul>"),
        ("S12_16_NestedLists_OlInUl", @"<ul style='list-style-type:disc;'><li>Outer<ol style='list-style-type:decimal;'><li>Numbered 1</li><li>Numbered 2</li></ol></li></ul>"),
        ("S12_17_ListItem_MarginPadding", @"<ul style='list-style-type:disc;margin-left:40px;'><li style='padding:5px;background-color:#eee;'>Padded item</li><li>Normal item</li></ul>"),
        ("S12_18_ListStyleType_Mixed", @"<ol><li style='list-style-type:upper-roman;'>Roman</li><li style='list-style-type:lower-alpha;'>Alpha</li><li style='list-style-type:decimal;'>Decimal</li></ol>"),
        ("S12_19_ListItem_Display", @"<div><span style='display:list-item;list-style-type:disc;margin-left:20px;'>Span as list item</span></div>"),
        ("S12_20_ListStyle_Inherit", @"<ul style='list-style-type:square;'><li>Parent square<ul><li>Inherited marker</li></ul></li></ul>"),
    ];

    /// <summary>CSS 2.1 Chapter 15 test snippets (20 tests).</summary>
    internal static readonly (string Name, string Html)[] Chapter15 =
    [
        ("S15_1_FontFamily_Serif", @"<p style='font-family:serif;'>Serif text sample</p>"),
        ("S15_2_FontFamily_SansSerif", @"<p style='font-family:sans-serif;'>Sans-serif text sample</p>"),
        ("S15_3_FontFamily_Monospace", @"<p style='font-family:monospace;'>Monospace text sample</p>"),
        ("S15_4_FontFamily_Cursive", @"<p style='font-family:cursive;'>Cursive text sample</p>"),
        ("S15_5_FontFamily_Fantasy", @"<p style='font-family:fantasy;'>Fantasy text sample</p>"),
        ("S15_6_FontFamily_Fallback", @"<p style='font-family:""Nonexistent Font"", Arial, sans-serif;'>Fallback font text</p>"),
        ("S15_7_FontStyle_Normal", @"<p style='font-style:normal;'>Normal style text</p>"),
        ("S15_8_FontStyle_Italic", @"<p style='font-style:italic;'>Italic style text</p>"),
        ("S15_9_FontStyle_Oblique", @"<p style='font-style:oblique;'>Oblique style text</p>"),
        ("S15_10_FontVariant_Normal", @"<p style='font-variant:normal;'>Normal variant text</p>"),
        ("S15_11_FontVariant_SmallCaps", @"<p style='font-variant:small-caps;'>Small Caps Variant Text</p>"),
        ("S15_12_FontWeight_Normal", @"<p style='font-weight:normal;'>Normal weight text</p>"),
        ("S15_13_FontWeight_Bold", @"<p style='font-weight:bold;'>Bold weight text</p>"),
        ("S15_14_FontWeight_Numeric", @"<p style='font-weight:100;'>100</p><p style='font-weight:400;'>400</p><p style='font-weight:700;'>700</p><p style='font-weight:900;'>900</p>"),
        ("S15_15_FontSize_AbsoluteKeywords", @"<p style='font-size:xx-small;'>xx-small</p><p style='font-size:small;'>small</p><p style='font-size:medium;'>medium</p><p style='font-size:large;'>large</p><p style='font-size:xx-large;'>xx-large</p>"),
        ("S15_16_FontSize_RelativeKeywords", @"<p style='font-size:14px;'><span style='font-size:larger;'>Larger</span> and <span style='font-size:smaller;'>Smaller</span></p>"),
        ("S15_17_FontSize_Px", @"<p style='font-size:12px;'>12px</p><p style='font-size:24px;'>24px</p><p style='font-size:36px;'>36px</p>"),
        ("S15_18_FontSize_Em", @"<div style='font-size:16px;'><p style='font-size:2em;'>2em (32px)</p><p style='font-size:0.5em;'>0.5em (8px)</p></div>"),
        ("S15_19_FontSize_Percentage", @"<div style='font-size:20px;'><p style='font-size:150%;'>150% of 20px</p><p style='font-size:50%;'>50% of 20px</p></div>"),
        ("S15_20_Font_Shorthand", @"<p style='font:italic small-caps bold 16px/1.5 serif;'>Font shorthand text</p>"),
    ];

    /// <summary>CSS 2.1 Chapter 16 test snippets (23 tests).</summary>
    internal static readonly (string Name, string Html)[] Chapter16 =
    [
        ("S16_1_TextIndent_Length", @"<p style='text-indent:40px;'>This paragraph has a 40px text indent applied to the first line.</p>"),
        ("S16_2_TextIndent_Percentage", @"<div style='width:200px;'><p style='text-indent:25%;'>25% indent of 200px container width.</p></div>"),
        ("S16_3_TextAlign_Left", @"<p style='text-align:left;width:300px;background-color:#eee;'>Left aligned text</p>"),
        ("S16_4_TextAlign_Right", @"<p style='text-align:right;width:300px;background-color:#eee;'>Right aligned text</p>"),
        ("S16_5_TextAlign_Center", @"<p style='text-align:center;width:300px;background-color:#eee;'>Center aligned text</p>"),
        ("S16_6_TextAlign_Justify", @"<p style='text-align:justify;width:200px;background-color:#eee;'>This is a justified paragraph with enough text to demonstrate word spacing adjustments across multiple lines.</p>"),
        ("S16_7_TextDecoration_Underline", @"<p style='text-decoration:underline;'>Underlined text</p>"),
        ("S16_8_TextDecoration_Overline", @"<p style='text-decoration:overline;'>Overlined text</p>"),
        ("S16_9_TextDecoration_LineThrough", @"<p style='text-decoration:line-through;'>Strikethrough text</p>"),
        ("S16_10_TextDecoration_None", @"<a href='#' style='text-decoration:none;'>Link with no underline</a>"),
        ("S16_11_TextDecoration_Multiple", @"<p style='text-decoration:underline overline;'>Underline and overline</p>"),
        ("S16_12_LetterSpacing_Normal", @"<p style='letter-spacing:normal;'>Normal letter spacing</p>"),
        ("S16_13_LetterSpacing_Px", @"<p style='letter-spacing:5px;'>Wide letter spacing</p><p style='letter-spacing:-1px;'>Tight letter spacing</p>"),
        ("S16_14_WordSpacing_Normal", @"<p style='word-spacing:normal;'>Normal word spacing between words</p>"),
        ("S16_15_WordSpacing_Px", @"<p style='word-spacing:20px;'>Wide word spacing here</p><p style='word-spacing:-2px;'>Tight word spacing here</p>"),
        ("S16_16_TextTransform_Capitalize", @"<p style='text-transform:capitalize;'>capitalize each word</p>"),
        ("S16_17_TextTransform_Uppercase", @"<p style='text-transform:uppercase;'>make everything uppercase</p>"),
        ("S16_18_TextTransform_Lowercase", @"<p style='text-transform:lowercase;'>MAKE EVERYTHING LOWERCASE</p>"),
        ("S16_19_WhiteSpace_Normal", @"<p style='white-space:normal;'>  Multiple   spaces   and
newlines   collapse  </p>"),
        ("S16_20_WhiteSpace_Pre", @"<p style='white-space:pre;'>  Preserved   spaces   and
newlines   kept  </p>"),
        ("S16_21_WhiteSpace_Nowrap", @"<div style='width:100px;border:1px solid black;'><p style='white-space:nowrap;'>This text will not wrap to the next line</p></div>"),
        ("S16_22_WhiteSpace_PreWrap", @"<div style='width:150px;border:1px solid black;'><p style='white-space:pre-wrap;'>  Preserved   spaces   that   also   wrap  </p></div>"),
        ("S16_23_WhiteSpace_PreLine", @"<p style='white-space:pre-line;'>  Spaces collapse but
newlines are preserved  </p>"),
    ];

    /// <summary>CSS 2.1 Chapter 6 test snippets (25 tests).</summary>
    internal static readonly (string Name, string Html)[] Chapter6 =
    [
        ("S6_1_1_CascadeYieldsValue_UsesIt", @"<style>div { background-color: red; }</style>
              <body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;'></div>
              </body>"),
        ("S6_1_1_InlineStyleOverridesStylesheet", @"<style>div { background-color: red; }</style>
              <body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;background-color:#00ff00;'></div>
              </body>"),
        ("S6_1_2_EmUnitsComputedToPx", @"<style>
                body { margin:0; padding:0; font-size:16px; }
                .box { width:2em; height:2em; background-color:red; }
              </style>
              <div class='box'></div>"),
        ("S6_1_2_PercentageDependsOnLayout", @"<style>
                body { margin:0; padding:0; }
                .container { width:200px; height:50px; }
                .half { width:50%; height:50px; background-color:red; }
              </style>
              <div class='container'>
                <div class='half'></div>
              </div>"),
        ("S6_1_2_InheritResolvesToParentComputed", @"<style>
                body { margin:0; padding:0; }
                .parent { background-color: red; width:100px; height:50px; }
                .child { background-color: inherit; width:50px; height:50px; }
              </style>
              <div class='parent'>
                <div class='child'></div>
              </div>"),
        ("S6_1_2_FontSizePercentageComputed", @"<style>
                body { margin:0; padding:0; font-size:20px; }
                .child { font-size:150%; width:1em; height:1em; background-color:red; }
              </style>
              <div class='child'></div>"),
        ("S6_1_2_EmCompoundsOnNesting", @"<style>
                body { margin:0; padding:0; font-size:10px; }
                .outer { font-size:2em; }
                .inner { font-size:2em; width:1em; height:1em; background-color:red; }
              </style>
              <div class='outer'>
                <div class='inner'></div>
              </div>"),
        ("S6_1_3_UsedValuesResolvePercentages", @"<style>
                body { margin:0; padding:0; }
                .container { width:400px; height:60px; }
                .quarter { width:25%; height:50px; background-color:red; }
              </style>
              <div class='container'>
                <div class='quarter'></div>
              </div>"),
        ("S6_1_3_PercentageMarginResolved", @"<style>
                body { margin:0; padding:0; }
                .container { width:400px; height:60px; }
                .box { margin-left:10%; width:50px; height:30px; background-color:red; }
              </style>
              <div class='container'>
                <div class='box'></div>
              </div>"),
        ("S6_1_4_IntegerRoundingForPixelValues", @"<style>
                body { margin:0; padding:0; }
                .box { width:33.3px; height:33.3px; background-color:red; }
              </style>
              <div class='box'></div>"),
        ("S6_2_NonInheritedPropertyUsesInitialValue", @"<style>
                body { margin:0; padding:0; }
                .parent { border:5px solid blue; width:200px; height:60px; }
                .child { width:100px; height:30px; background-color:red; margin:10px; }
              </style>
              <div class='parent'>
                <div class='child'></div>
              </div>"),
        ("S6_2_NonInheritedBackgroundNotPassed", @"<style>
                body { margin:0; padding:0; }
                .parent { background-color:blue; width:200px; height:100px; }
                .child { background-color:red; width:100px; height:50px; }
              </style>
              <div class='parent'>
                <div class='child'></div>
              </div>"),
        ("S6_2_LineHeightInherited", @"<style>
                body { margin:0; padding:0; line-height:40px; font-size:16px; }
                .box { background-color:red; width:200px; }
              </style>
              <div class='box'><span>A</span></div>"),
        ("S6_2_1_InheritKeywordForcesInheritance", @"<style>
                body { margin:0; padding:0; }
                .parent { background-color:red; width:200px; height:60px; }
                .child { background-color:inherit; width:100px; height:30px; }
              </style>
              <div class='parent'>
                <div class='child'></div>
              </div>"),
        ("S6_2_1_InheritApplesToInheritedAndNonInherited", @"<style>
                body { margin:0; padding:0; }
                .parent { color: red; background-color: blue; width:200px; height:80px; }
                .child { color: inherit; background-color: inherit;
                         width:100px; height:40px; }
              </style>
              <div class='parent'>
                <div class='child'></div>
              </div>"),
        ("S6_2_1_InheritMarginNonInherited", @"<style>
                body { margin:0; padding:0; }
                .parent { margin-left:40px; width:200px; height:60px; background-color:#00ff00; }
                .child { margin-left:inherit; width:50px; height:30px; background-color:red; }
              </style>
              <div class='parent'>
                <div class='child'></div>
              </div>"),
        ("S6_4_1_LaterDeclarationWins_SourceOrder", @"<style>
                div { background-color: red; }
                div { background-color: blue; }
              </style>
              <body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;'></div>
              </body>"),
        ("S6_4_1_HigherSpecificityWins", @"<style>
                div { background-color: red; }
                div.special { background-color: blue; }
              </style>
              <body style='margin:0;padding:0;'>
                <div class='special' style='width:100px;height:50px;'></div>
              </body>"),
        ("S6_4_1_InlineStyleHighestSpecificity", @"<style>
                #myid { background-color: red; }
                div.special { background-color: yellow; }
              </style>
              <body style='margin:0;padding:0;'>
                <div id='myid' class='special'
                     style='width:100px;height:50px;background-color:blue;'></div>
              </body>"),
        ("S6_4_1_AuthorOverridesUADefaults", @"<style>
                body { margin:0; padding:0; background-color:red; }
              </style>
              <body></body>"),
        ("S6_4_3_IdSelectorSpecificity", @"<style>
                .cls { background-color: red; }
                #myid { background-color: blue; }
              </style>
              <body style='margin:0;padding:0;'>
                <div id='myid' class='cls' style='width:100px;height:50px;'></div>
              </body>"),
        ("S6_4_3_ClassSelectorSpecificity", @"<style>
                div { background-color: red; }
                .highlight { background-color: blue; }
              </style>
              <body style='margin:0;padding:0;'>
                <div class='highlight' style='width:100px;height:50px;'></div>
              </body>"),
        ("S6_4_3_UniversalSelectorSpecificityZero", @"<style>
                * { background-color: red; }
                div { background-color: blue; }
              </style>
              <body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;'></div>
              </body>"),
        ("S6_4_3_CombinatorsDoNotAffectSpecificity", @"<style>
                body { margin:0; padding:0; }
                div p { background-color: red; }
                div > p { background-color: blue; }
              </style>
              <div>
                <p style='width:100px;height:50px;margin:0;'>text</p>
              </div>"),
        ("S6_4_4_PresentationalHintTreatedAsAuthorSpec0", @"<body style='margin:0;padding:0;'>
                <table bgcolor='red' style='border-collapse:collapse;'>
                  <tr><td style='width:100px;height:50px;'>X</td></tr>
                </table>
              </body>"),
    ];

    /// <summary>CSS 2.1 Chapter 13 test snippets (25 tests).</summary>
    internal static readonly (string Name, string Html)[] Chapter13 =
    [
        ("S13_1_ContinuousMediaRendersNormally", @"<div style='width:200px;height:100px;background-color:red;'>Continuous</div>"),
        ("S13_1_PageBoxConceptNotApplied", @"<div style='margin:10px;border:2px solid black;padding:5px;width:150px;height:80px;
                          background-color:blue;'>Page-box concept</div>"),
        ("S13_2_PageBoxModelIgnoredInContinuousMedia", @"<style>@page { margin: 1in; }</style>
              <div style='width:100px;height:50px;background-color:red;'>Box model test</div>"),
        ("S13_2_PageAreaContentRendersNormally", @"<style>@page { size: 210mm 297mm; margin: 25mm; }</style>
              <div style='width:150px;height:50px;background-color:blue;'>Page area</div>"),
        ("S13_2_1_PageMarginShorthand", @"<style>@page { margin: 1cm 2cm 3cm 4cm; }</style>
              <div style='width:100px;height:50px;background-color:green;'>Shorthand</div>"),
        ("S13_2_3_ContentOverflowPageArea", @"<style>@page { margin: 5cm; }</style>
              <div style='width:800px;height:400px;background-color:red;'>
                Large content that overflows
              </div>"),
        ("S13_2_3_ContentOutsidePageBoxNotClipped", @"<style>@page { margin: 10cm; }</style>
              <div style='width:100px;height:50px;background-color:green;'>Visible</div>"),
        ("S13_3_1_PageBreakBefore_Auto", @"<div style='page-break-before:auto;width:100px;height:50px;background-color:red;'></div>"),
        ("S13_3_1_PageBreakBefore_Always", @"<div style='page-break-before:always;width:100px;height:50px;background-color:red;'></div>"),
        ("S13_3_1_PageBreakBefore_Avoid", @"<div style='page-break-before:avoid;width:100px;height:50px;background-color:blue;'></div>"),
        ("S13_3_1_PageBreakAfter_Always", @"<div style='page-break-after:always;width:100px;height:50px;background-color:red;'></div>"),
        ("S13_3_1_PageBreakAfter_Avoid", @"<div style='page-break-after:avoid;width:100px;height:50px;background-color:blue;'></div>"),
        ("S13_3_1_PageBreakInside_Avoid", @"<div style='page-break-inside:avoid;width:100px;height:50px;background-color:blue;'></div>"),
        ("S13_3_1_MultiplePageBreakProperties", @"<div style='page-break-before:always;page-break-after:avoid;page-break-inside:avoid;
                          width:150px;height:80px;background-color:red;'>All three</div>"),
        ("S13_3_3_BreakBetweenAdjacentBlocks", @"<div style='width:200px;'>
                <div style='height:50px;background-color:red;page-break-after:always;'></div>
                <div style='height:50px;background-color:blue;'></div>
              </div>"),
        ("S13_3_3_NoBreakInsideTable", @"<table style='page-break-inside:avoid;border:1px solid black;'>
                <tr><td>Cell 1</td><td>Cell 2</td></tr>
                <tr><td>Cell 3</td><td>Cell 4</td></tr>
              </table>"),
        ("S13_3_3_NoBreakInsideAbsolutelyPositioned", @"<div style='position:relative;width:300px;height:200px;'>
                <div style='position:absolute;top:10px;left:10px;width:100px;height:80px;
                            background-color:red;page-break-inside:avoid;'>Abs</div>
              </div>"),
        ("S13_3_4_ForcedBreakAlways", @"<div style='width:200px;'>
                <div style='height:40px;background-color:red;page-break-after:always;'>Before</div>
                <div style='height:40px;background-color:blue;'>After</div>
              </div>"),
        ("S13_3_4_BlankPageNotInsertedInContinuousMedia", @"<div style='width:200px;'>
                <div style='height:30px;background-color:red;page-break-after:left;'>A</div>
                <div style='height:30px;background-color:green;page-break-after:right;'>B</div>
                <div style='height:30px;background-color:blue;'>C</div>
              </div>"),
        ("S13_3_4_ForcedBreakBetweenSiblings", @"<div style='width:200px;'>
                <p style='page-break-after:always;background-color:red;'>Sibling A</p>
                <p style='background-color:blue;'>Sibling B</p>
              </div>"),
        ("S13_3_5_PreferBreakBetweenBlocks", @"<div style='width:200px;'>
                <div style='height:60px;background-color:red;page-break-inside:avoid;'>Block A</div>
                <div style='height:60px;background-color:blue;page-break-inside:avoid;'>Block B</div>
              </div>"),
        ("S13_4_PageContextCascadeOrder", @"<style>
                @page { margin-top: 1cm; }
                @page { margin-top: 5cm; }
              </style>
              <div style='width:100px;height:50px;background-color:green;'>Cascade order</div>"),
        ("S13_3_1_PageBreakBefore_FirstChild", @"<div style='width:200px;'>
                <p style='page-break-before:always;background-color:red;'>First child with break</p>
                <p>Second child</p>
              </div>"),
        ("S13_3_1_PageBreakAfter_LastChild", @"<div style='width:200px;'>
                <p>First child</p>
                <p style='page-break-after:always;background-color:blue;'>Last child with break</p>
              </div>"),
        ("S13_Combined_AtPageWithPageBreakProperties", @"<style>@page { margin: 2cm; }</style>
              <div style='page-break-before:always;width:200px;'>
                <p style='page-break-after:avoid;'>First paragraph</p>
                <p>Second paragraph</p>
              </div>"),
    ];

    /// <summary>All CSS2 test snippets across all chapters.</summary>
    /// <remarks>
    /// Each snippet is wrapped in <c>&lt;html&gt;&lt;body&gt;…&lt;/body&gt;&lt;/html&gt;</c>
    /// when not already present. This aligns html-renderer's fragment parsing
    /// with Chromium's implicit HTML5 wrapper injection so that the UA default
    /// <c>body { margin: 8px }</c> applies consistently in both engines during
    /// differential comparison (Phase 1 — P1 UA stylesheet alignment).
    /// </remarks>
    internal static IEnumerable<(string Chapter, string Name, string Html)> All()
    {
        foreach (var (name, html) in Chapter6)
            yield return ("Chapter 6", name, EnsureBodyWrapper(html));
        foreach (var (name, html) in Chapter9)
            yield return ("Chapter 9", name, EnsureBodyWrapper(html));
        foreach (var (name, html) in Chapter10)
            yield return ("Chapter 10", name, EnsureBodyWrapper(html));
        foreach (var (name, html) in Chapter12)
            yield return ("Chapter 12", name, EnsureBodyWrapper(html));
        foreach (var (name, html) in Chapter13)
            yield return ("Chapter 13", name, EnsureBodyWrapper(html));
        foreach (var (name, html) in Chapter15)
            yield return ("Chapter 15", name, EnsureBodyWrapper(html));
        foreach (var (name, html) in Chapter16)
            yield return ("Chapter 16", name, EnsureBodyWrapper(html));
        foreach (var (name, html) in Chapter17)
            yield return ("Chapter 17", name, EnsureBodyWrapper(html));
    }

    /// <summary>
    /// Wraps HTML in <c>&lt;html&gt;&lt;body&gt;…&lt;/body&gt;&lt;/html&gt;</c>
    /// when neither <c>&lt;body</c> nor <c>&lt;html</c> tags are present.
    /// Snippets that already contain explicit wrappers are returned unchanged.
    /// </summary>
    internal static string EnsureBodyWrapper(string html)
    {
        if (html.Contains("<body", StringComparison.OrdinalIgnoreCase) ||
            html.Contains("<html", StringComparison.OrdinalIgnoreCase))
            return html;

        return $"<html><body>{html}</body></html>";
    }
}
