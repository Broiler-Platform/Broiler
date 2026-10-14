// Bring the platform-neutral graphics enums in under their historical simple
// names so test call sites compile after the move off System.Drawing.Common.
global using FontStyle = Broiler.Graphics.Text.FontStyle;
global using DashStyle = Broiler.Graphics.Rendering.DashStyle;
