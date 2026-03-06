using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Broiler.DevSite.Pages;

public class PlaygroundModel : PageModel
{
    public string DefaultSnippet { get; } = """
        <!DOCTYPE html>
        <html>
        <head>
            <style>
                body { font-family: sans-serif; margin: 20px; }
                h1 { color: #333; }
                .box {
                    width: 200px;
                    height: 100px;
                    background: #4CAF50;
                    color: white;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    border-radius: 8px;
                    margin-top: 10px;
                }
            </style>
        </head>
        <body>
            <h1>Hello from Broiler!</h1>
            <p>Edit this HTML and click Render to see the output.</p>
            <div class="box">Styled Box</div>
        </body>
        </html>
        """;

    public void OnGet() { }
}
