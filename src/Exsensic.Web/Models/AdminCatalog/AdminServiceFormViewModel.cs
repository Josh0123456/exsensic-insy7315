using System.ComponentModel.DataAnnotations;
using Exsensic.Web.Models.Journeys;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Exsensic.Web.Models.AdminCatalog;

/// <summary>
/// The create/edit service form. <see cref="Id"/> is null for create.
/// </summary>
public sealed class AdminServiceFormViewModel : JourneyPage
{

    public int? Id { get; set; }

   
    [Required, MaxLength(120), Display(Name = "Name")]
    public string Name { get; set; } = "";


    [Required, Display(Name = "Category")]
    public string Category { get; set; } = "Photoshoot";


    [Required, MaxLength(2000), Display(Name = "Description")]
    public string Description { get; set; } = "";

    [Range(15, 480), Display(Name = "Duration (minutes)")]
    public int DurationMinutes { get; set; } = 60;

    [Display(Name = "Starting price")]
    public decimal? BasePrice { get; set; }

 
    [BindNever]
    public string? RowVersion { get; set; }

    [BindNever]
    public IReadOnlyList<string> Categories { get; } = ["Photoshoot", "Website", "Instagram", "TikTok"];
}
