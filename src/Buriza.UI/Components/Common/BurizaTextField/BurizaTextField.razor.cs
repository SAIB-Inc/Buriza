using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using System.Linq.Expressions;

namespace Buriza.UI.Components.Common;

public partial class BurizaTextField
{
    [Parameter]
    public bool Disabled { get; set; } = false;

    [Parameter]
    public bool Immediate { get; set; } = false;

    [Parameter]
    public string Class { get; set; } = string.Empty;

    [Parameter]
    public string Value { get; set; } = string.Empty;

    [Parameter]
    public string Placeholder { get; set; } = string.Empty;

    [Parameter]
    public string Icon { get; set; } = string.Empty;

    [Parameter]
    public int Lines { get; set; }

    [Parameter]
    public Adornment Adornment { get; set; } = Adornment.End;

    [Parameter]
    public Variant Variant { get; set; } = Variant.Outlined;

    [Parameter]
    public Expression<Func<string>>? For { get; set; }

    [Parameter]
    public EventCallback<FocusEventArgs> OnBlur { get; set; }

    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    private string TextFieldClass => $"""
        w-full !text-[var(--mud-palette-text-primary)] [&_.mud-input-outlined-border]:!border-none !rounded-[12px]
        border border-gray-300 !bg-[var(--mud-palette-gray-default)] [&_input]:!pl-5 [&_.mud-input]:!pr-5 
        [&_input::placeholder]:!opacity-100
        {Class}
    """;
}
