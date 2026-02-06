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
    public string Label { get; set; } = string.Empty;

    [Parameter]
    public string Placeholder { get; set; } = string.Empty;

    [Parameter]
    public string HelperText { get; set; } = string.Empty;

    [Parameter]
    public string Icon { get; set; } = string.Empty;

    [Parameter]
    public int Lines { get; set; }

    [Parameter]
    public Adornment Adornment { get; set; } = Adornment.End;

    [Parameter]
    public Variant Variant { get; set; } = Variant.Filled;

    [Parameter]
    public Expression<Func<string>>? For { get; set; }

    [Parameter]
    public EventCallback<FocusEventArgs> OnBlur { get; set; }

    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    private string TextFieldClass => $"""
        w-full !text-[var(--mud-palette-text-primary)]
        [&_.mud-input-control-input-container]:!bg-[var(--mud-palette-gray-darker)] [&_.mud-input-control-input-container]:!border-none [&_.mud-input-control-input-container]:!rounded-lg [&_.mud-input-control-input-container]:!rounded-lg
        [&_.mud-input.mud-input-filled]:!bg-transparent
        [&_.mud-input.mud-input-filled.mud-input-underline:before]:!border-b-0 
        [&_.mud-input.mud-input-filled.mud-input-underline:after]:!border-b-0
        [&_.mud-input-label]:!mb-[4px]
        [&_.mud-input-helper-text]:!text-[var(--mud-palette-dark-lighten)]
        [&_.mud-input-control-helper-container]:!pl-4
        [&_input]:!text-base [&_input]:!caret-blue-500 [&_input]:!text-base [&_input]:!pt-[24px] [&_input]:!pb-[8px] [&_input::placeholder]:!text-[var(--mud-palette-dark-lighten)] 
        {Class}
    """;
}
