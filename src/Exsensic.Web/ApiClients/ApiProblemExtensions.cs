using Exsensic.Web.Models.Shared;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Exsensic.Web.ApiClients;

/// <summary>Connects safe API feedback to standard MVC field summaries and the existing toast partial.</summary>
public static class ApiProblemExtensions
{
    /// <summary>Adds the friendly summary and field messages, with an optional MVC binding prefix.</summary>
    public static void AddToModelState(this ApiProblem problem, ModelStateDictionary modelState, string prefix = "")
    {
        modelState.AddModelError(string.Empty, problem.Message);
        foreach (var (field, messages) in problem.FieldErrors)
        {
            var key = string.IsNullOrEmpty(field) ? string.Empty
                : string.IsNullOrEmpty(prefix) ? field : $"{prefix}.{field}";
            foreach (var message in messages)
            {
                modelState.AddModelError(key, message);
            }
        }
    }

    /// <summary>Stores only the safe summary for the existing, Razor-encoded error toast after redirect.</summary>
    public static void AddToTempData(this ApiProblem problem, ITempDataDictionary tempData)
    {
        tempData[ToastKeys.Error] = problem.Message;
    }
}
