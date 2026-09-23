using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using GamebuinoAKA.App.ViewModels;

namespace GamebuinoAKA.App
{
    /// <summary>Associe un ViewModel à sa Vue par convention de nom (…ViewModel → …View).</summary>
    public class ViewLocator : IDataTemplate
    {
        public Control Build(object? data)
        {
            if (data is null) return new TextBlock { Text = "null" };
            var name = data.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
            var type = Type.GetType(name);
            return type != null
                ? (Control)Activator.CreateInstance(type)!
                : new TextBlock { Text = "Vue introuvable : " + name };
        }

        public bool Match(object? data) => data is ViewModelBase;
    }
}
