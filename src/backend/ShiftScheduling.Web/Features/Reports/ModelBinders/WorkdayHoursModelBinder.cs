using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ShiftScheduling.Features.Reports.ModelBinders;

/// <summary>
/// Xử lý riêng giá trị giờ công vì ô nhập số của trình duyệt sử dụng dấu chấm,
/// trong khi văn hóa vi-VN sử dụng dấu phẩy làm dấu thập phân.
/// Chấp nhận một dấu chấm hoặc dấu phẩy để tránh diễn giải sai giá trị.
/// </summary>
public sealed class WorkdayHoursModelBinder : IModelBinder
{
    private const NumberStyles AllowedStyles = NumberStyles.AllowDecimalPoint
        | NumberStyles.AllowLeadingWhite
        | NumberStyles.AllowTrailingWhite
        | NumberStyles.AllowLeadingSign;

    private const string FormatErrorMessage = "Số giờ làm việc không đúng định dạng số.";

    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var valueResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (valueResult == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueResult);
        var rawValue = valueResult.FirstValue;

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            bindingContext.Result = ModelBindingResult.Success(null);
            return Task.CompletedTask;
        }

        var trimmed = rawValue.Trim();
        int dotCount = trimmed.Count(c => c == '.');
        int commaCount = trimmed.Count(c => c == ',');

        if ((dotCount > 0 && commaCount > 0) || dotCount > 1 || commaCount > 1)
        {
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, FormatErrorMessage);
            return Task.CompletedTask;
        }

        var normalized = trimmed.Replace(',', '.');
        if (decimal.TryParse(normalized, AllowedStyles, CultureInfo.InvariantCulture, out var parsedValue))
        {
            bindingContext.Result = ModelBindingResult.Success(parsedValue);
        }
        else
        {
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, FormatErrorMessage);
        }

        return Task.CompletedTask;
    }
}
