using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using ShiftScheduling.Features.Reports.ModelBinders;
using ShiftScheduling.Features.Reports.ViewModels;

namespace ShiftScheduling.Tests.Reports;

public sealed class WorkdayHoursModelBinderTests
{
    private static DefaultModelBindingContext CreateBindingContext(string? rawValue, string modelName = "ActualWorkedHours")
    {
        var modelState = new ModelStateDictionary();
        var values = new Dictionary<string, string>();
        if (rawValue != null)
        {
            values[modelName] = rawValue;
        }

        return new DefaultModelBindingContext
        {
            ModelName = modelName,
            ModelState = modelState,
            ValueProvider = new SimpleTestValueProvider(values)
        };
    }

    [Theory(DisplayName = "TC-REPORT-005 - Nhập 2.0 hoặc 2,0 được hiểu đúng là 2 giờ, không bị thành 20 giờ")]
    [InlineData("2.0")]
    [InlineData("2,0")]
    public async Task BindModelAsync_WhenInputIsTwoWithDecimalPointOrComma_BindsSuccessfullyAsTwoDecimal(string input)
    {
        // Arrange
        var binder = new WorkdayHoursModelBinder();
        var context = CreateBindingContext(input);

        // Act
        await binder.BindModelAsync(context);

        // Assert
        Assert.True(context.Result.IsModelSet);
        Assert.Equal(2m, context.Result.Model);
        Assert.Equal(0, context.ModelState.ErrorCount);
    }

    [Theory(DisplayName = "TC-REPORT-006 - Nhập 3.5 hoặc 3,5 được hiểu đúng là 3,5 giờ, không bị thành 35 giờ")]
    [InlineData("3.5")]
    [InlineData("3,5")]
    public async Task BindModelAsync_WhenInputIsThreeAndHalfWithDecimalPointOrComma_BindsSuccessfullyAsThreePointFiveDecimal(string input)
    {
        // Arrange
        var binder = new WorkdayHoursModelBinder();
        var context = CreateBindingContext(input);

        // Act
        await binder.BindModelAsync(context);

        // Assert
        Assert.True(context.Result.IsModelSet);
        Assert.Equal(3.5m, context.Result.Model);
        Assert.Equal(0, context.ModelState.ErrorCount);
    }

    [Fact(DisplayName = "TC-REPORT-007 - Nhập số nguyên 2 được hiểu đúng là 2 giờ")]
    public async Task BindModelAsync_WhenInputIsIntegerString_BindsSuccessfullyAsDecimal()
    {
        // Arrange
        var binder = new WorkdayHoursModelBinder();
        var context = CreateBindingContext("2");

        // Act
        await binder.BindModelAsync(context);

        // Assert
        Assert.True(context.Result.IsModelSet);
        Assert.Equal(2m, context.Result.Model);
        Assert.Equal(0, context.ModelState.ErrorCount);
    }

    [Theory(DisplayName = "TC-REPORT-008 - Để trống giờ công thì hệ thống hiểu là không có giá trị điều chỉnh")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BindModelAsync_WhenInputIsEmptyOrWhitespace_BindsSuccessfullyAsNull(string input)
    {
        // Arrange
        var binder = new WorkdayHoursModelBinder();
        var context = CreateBindingContext(input);

        // Act
        await binder.BindModelAsync(context);

        // Assert
        Assert.True(context.Result.IsModelSet);
        Assert.Null(context.Result.Model);
        Assert.Equal(0, context.ModelState.ErrorCount);
    }

    [Theory(DisplayName = "TC-REPORT-009 - Định dạng giờ công không hợp lệ hoặc có dấu phân cách nhập nhằng bị từ chối")]
    [InlineData("abc")]
    [InlineData("1.000,5")]
    [InlineData("1,000.5")]
    [InlineData("1.2.3")]
    [InlineData("1,2,3")]
    [InlineData("1 000")]
    public async Task BindModelAsync_WhenInputIsInvalidOrContainsAmbiguousSeparators_AddsModelError(string input)
    {
        // Arrange
        var binder = new WorkdayHoursModelBinder();
        var context = CreateBindingContext(input);

        // Act
        await binder.BindModelAsync(context);

        // Assert
        Assert.False(context.Result.IsModelSet);
        Assert.True(context.ModelState.ErrorCount > 0);
        Assert.True(context.ModelState.ContainsKey("ActualWorkedHours"));
    }

    [Fact(DisplayName = "TC-REPORT-010 - Trường giờ công thực tế sử dụng cách xử lý số thập phân riêng")]
    public void ActualWorkedHoursProperty_HasWorkdayHoursModelBinderAttribute()
    {
        // Arrange
        var propertyInfo = typeof(AdjustWorkdayHoursRequest).GetProperty(nameof(AdjustWorkdayHoursRequest.ActualWorkedHours));

        // Act
        var attribute = propertyInfo?.GetCustomAttribute<ModelBinderAttribute>();

        // Assert
        Assert.NotNull(attribute);
        Assert.Equal(typeof(WorkdayHoursModelBinder), attribute.BinderType);
    }

    [Fact(DisplayName = "TC-REPORT-011 - Khi biểu mẫu không gửi giờ công thì hệ thống không tự gán giá trị mặc định")]
    public async Task BindModelAsync_WhenFieldHasNoValue_DoesNotSetModel()
    {
        // Arrange
        var binder = new WorkdayHoursModelBinder();
        var context = CreateBindingContext(rawValue: null);

        // Act
        await binder.BindModelAsync(context);

        // Assert
        Assert.False(context.Result.IsModelSet);
        Assert.Equal(0, context.ModelState.ErrorCount);
    }

    private sealed class SimpleTestValueProvider : IValueProvider
    {
        private readonly Dictionary<string, string> _values;

        public SimpleTestValueProvider(Dictionary<string, string> values)
        {
            _values = values;
        }

        public bool ContainsPrefix(string prefix) => _values.ContainsKey(prefix);

        public ValueProviderResult GetValue(string key) =>
            _values.TryGetValue(key, out var val)
                ? new ValueProviderResult(val)
                : ValueProviderResult.None;
    }
}
