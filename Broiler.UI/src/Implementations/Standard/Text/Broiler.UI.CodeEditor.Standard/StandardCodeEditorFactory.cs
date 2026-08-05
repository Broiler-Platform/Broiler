using System;

namespace Broiler.UI.CodeEditor.Standard;

public sealed class StandardCodeEditorFactory : IUiElementFactory
{
    public Type ContractType => typeof(UiCodeEditor);

    public UiElement Create(UiElementFactoryContext context) => new StandardCodeEditor();
}
