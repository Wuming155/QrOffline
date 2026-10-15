using QRCoder;
using QRCoder.Exceptions;
using Xunit;

namespace QrOffline.Tests;

public class QrEncodingTests
{
    [Fact]
    public void Utf8ContentEncodesAsExpected()
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode("离线二维码生成器", QRCodeGenerator.ECCLevel.M, forceUtf8: true, utf8BOM: false);

        Assert.NotNull(data.ModuleMatrix);
        Assert.NotEmpty(data.ModuleMatrix);
    }

    [Fact]
    public void OversizedContentThrowsDataTooLongException()
    {
        // 这是 MainViewModel 依赖的异常类型：超长内容会抛出 DataTooLongException。
        using var generator = new QRCodeGenerator();

        Assert.Throws<DataTooLongException>(() =>
            generator.CreateQrCode(new string('A', 8000), QRCodeGenerator.ECCLevel.H, forceUtf8: true, utf8BOM: false));
    }
}
