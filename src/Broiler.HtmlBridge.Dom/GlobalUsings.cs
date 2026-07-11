// RF-BRIDGE-1c Phase F4 (final cutover): the compatibility facade
// Broiler.HtmlBridge.DomElement has been deleted. The bridge builds its entire tree
// from canonical Broiler.Dom nodes now; this alias re-points every unqualified
// `DomElement` in this assembly at the canonical type so the ~900 element-handling
// sites resolve to Broiler.Dom.DomElement without per-site edits. Element construction
// funnels through DomBridge.CreateBridgeElement / CreateBridgeElementNS over the
// document factories (CreateElement / CreateElementNS).
global using DomElement = Broiler.Dom.DomElement;
