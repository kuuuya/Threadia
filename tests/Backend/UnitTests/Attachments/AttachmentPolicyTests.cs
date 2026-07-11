using FluentAssertions;
using Threadia.Modules.Attachments.Domain;
using Xunit;

namespace Threadia.UnitTests.Attachments;

public class AttachmentPolicyTests
{
    [Fact]
    public void 有効なファイル_サニタイズ済みファイル名を返す()
    {
        AttachmentPolicy.Validate("photo.png", "image/png", 1024).Should().Be("photo.png");
    }

    [Fact]
    public void パス区切りを含むファイル名_ファイル名部分のみ残す()
    {
        AttachmentPolicy.Validate(@"..\..\evil/dir/photo.png", "image/png", 1024).Should().Be("photo.png");
    }

    [Fact]
    public void サイズ上限超過_例外を送出する()
    {
        var act = () => AttachmentPolicy.Validate("big.png", "image/png", AttachmentPolicy.MaxSizeBytes + 1);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("app.exe")]
    [InlineData("script.bat")]
    [InlineData("noextension")]
    public void 許可されていない拡張子_例外を送出する(string fileName)
    {
        var act = () => AttachmentPolicy.Validate(fileName, "application/octet-stream", 1024);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void 許可されていないContentType_例外を送出する()
    {
        var act = () => AttachmentPolicy.Validate("file.zip", "application/x-msdownload", 1024);

        act.Should().Throw<ArgumentException>();
    }
}
