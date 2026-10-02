using Avalonia.Controls;
using Avalonia.Media.Imaging;
using QRCoder;

namespace ClassFabric.RemoteBroadcast.UI;

/// <summary>
/// 接入码二维码弹窗（R3）：URL 由「设置页选定的网卡 IP + 端口 + 接入码」拼成，
/// IP/端口变更后重新点开二维码即拿到新图——不搞常驻监听刷新，简单直接。
/// </summary>
public partial class QrWindow : Window
{
    public QrWindow(string note, string url)
    {
        InitializeComponent();
        NoteText.Text = note;
        UrlText.Text = url;

        var generator = new QRCodeGenerator();
        var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data).GetGraphic(6);
        QrImage.Source = new Bitmap(new MemoryStream(png));
    }
}
