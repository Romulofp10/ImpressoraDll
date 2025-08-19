using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ImpressoraDll
{
    public class Printer
    {
        // Inicializa a impressora (ESC @)
        byte[] init = new byte[] { 0x1B, 0x40 };
        // Alimenta 3 linhas
        byte[] feed = new byte[] { 0x0A, 0x0A, 0x0A };
        // Comando de corte (corte total)
        byte[] cut = new byte[] { 0x1D, 0x56, 0x00 };

        // 🔹 Converte Base64 em ESC/POS
        public static byte[] Base64ParaEscPos(string base64)
        {
            if (string.IsNullOrEmpty(base64))
                return Array.Empty<byte>();

            byte[] imageBytes = Convert.FromBase64String(base64);

            using (MemoryStream ms = new MemoryStream(imageBytes))
            using (Bitmap bmpOriginal = new Bitmap(ms))
            {
                int maxWidth = 576; // largura máx 80mm
                Bitmap bmpRedimensionado = bmpOriginal;

                if (bmpOriginal.Width > maxWidth)
                {
                    int newHeight = (int)((double)bmpOriginal.Height / bmpOriginal.Width * maxWidth);
                    bmpRedimensionado = new Bitmap(bmpOriginal, new Size(maxWidth, newHeight));
                }

                byte[] escposBytes = ConverterBitmapParaEscPos(bmpRedimensionado);

                if (bmpRedimensionado != bmpOriginal)
                    bmpRedimensionado.Dispose();

                return escposBytes;
            }
        }

        // 🔹 Converte QRCode em ESC/POS
        public static byte[] GerarQrCodeEscPos(string data, int tamanhoModulo = 6 /*1..16*/, byte nivelCorrecao = 0x31 /*M*/)
        {
            // nivelCorrecao: 0x30=L, 0x31=M, 0x32=Q, 0x33=H
            if (string.IsNullOrWhiteSpace(data)) return Array.Empty<byte>();

            var bytes = new List<byte>();

            // (opcional) alinhar centro
            bytes.AddRange(new byte[] { 0x1B, 0x61, 0x01 });

            // 1) Seleciona modelo (Model 2)
            bytes.AddRange(new byte[] { 0x1D, 0x28, 0x6B, 0x04, 0x00, 0x31, 0x41, 0x32, 0x00 });

            // 2) Tamanho do módulo (1..16)
            if (tamanhoModulo < 1) tamanhoModulo = 1;
            if (tamanhoModulo > 16) tamanhoModulo = 16;
            bytes.AddRange(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x43, (byte)tamanhoModulo });

            // 3) Nível de correção de erro
            bytes.AddRange(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x45, nivelCorrecao });

            // 4) Armazena dados
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);
            int len = dataBytes.Length + 3;
            byte pL = (byte)(len & 0xFF);
            byte pH = (byte)((len >> 8) & 0xFF);
            bytes.AddRange(new byte[] { 0x1D, 0x28, 0x6B, pL, pH, 0x31, 0x50, 0x30 });
            bytes.AddRange(dataBytes);

            // 5) Imprime
            bytes.AddRange(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x51, 0x30 });

            // (opcional) linha após
            bytes.Add(0x0A);

            return bytes.ToArray();
        }

        // 🔹 Conversão Bitmap → ESC/POS
        private static byte[] ConverterBitmapParaEscPos(Bitmap bmp)
        {
            int maxWidth = 384; // reduz p/ velocidade
            int newHeight = (int)((double)bmp.Height / bmp.Width * maxWidth);
            Bitmap resized = new Bitmap(bmp, new Size(maxWidth, newHeight));

            List<byte> bytes = new List<byte>();

            for (int y = 0; y < resized.Height; y += 8)
            {
                bytes.Add(0x1B);
                bytes.Add(0x2A);
                bytes.Add(0x00); // modo 8-dot single density
                bytes.Add((byte)(resized.Width & 0xFF));
                bytes.Add((byte)(resized.Width >> 8));

                for (int x = 0; x < resized.Width; x++)
                {
                    byte columnByte = 0;
                    for (int bit = 0; bit < 8; bit++)
                    {
                        int yPos = y + bit;
                        if (yPos >= resized.Height) break;

                        Color c = resized.GetPixel(x, yPos);
                        int gray = (c.R + c.G + c.B) / 3;
                        if (gray < 128)
                        {
                            columnByte |= (byte)(1 << (7 - bit));
                        }
                    }
                    bytes.Add(columnByte);
                }
                bytes.Add(0x0A);
            }

            return bytes.ToArray();
        }

        private byte[] convertTextToByte(string text)
        {
            return Encoding.ASCII.GetBytes(text + "\n");
        }

        private byte[] ConcactBytes(params byte[][] arrays)
        {
            int totalLength = 0;
            foreach (var arr in arrays)
                totalLength += arr.Length;

            byte[] result = new byte[totalLength];
            int offset = 0;
            foreach (var arr in arrays)
            {
                Buffer.BlockCopy(arr, 0, result, offset, arr.Length);
                offset += arr.Length;
            }
            return result;
        }

        public Task<object> Print(dynamic input)
        {
            string printName = (string)input.printName;
            string text = (string)input.text;

            var dict = (IDictionary<string, object>)input;

            string image = dict.ContainsKey("image") ? (string)input.image : "";
            string qrCode = dict.ContainsKey("qrCode") ? (string)input.qrCode : "";
            int widthQrCode = dict.ContainsKey("widthQrCode") ? (int)input.widthQrCode : 8;

            byte[] data = convertTextToByte(text);
            byte[] imageBytes = !string.IsNullOrEmpty(image) ? Base64ParaEscPos(image) : Array.Empty<byte>();
            byte[] qrBytes = !string.IsNullOrWhiteSpace(qrCode) ? GerarQrCodeEscPos(qrCode, tamanhoModulo: widthQrCode, nivelCorrecao: 0x31) : Array.Empty<byte>();


            // 🔹 Monta buffer final
            byte[] bufferFinal = ConcactBytes(init, data, imageBytes, qrBytes);

            Console.WriteLine($"[DEBUG] Impressão: Printer={printName}, Texto={text}, TemImagem={imageBytes.Length > 0}, TemQRCode={qrBytes.Length > 0}");

            try
            {
                RawPrinterHelper.SendRaw(printName, bufferFinal);
                return Task.FromResult<object>(input);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro ao imprimir: " + ex);
                return null;
            }
        }

        public void Text(dynamic input)
        {
            string printName = (string)input.printName;
            string text = (string)input.text;
            byte[] convertedTextToByte = convertTextToByte(text);
            try
            {
                RawPrinterHelper.SendRaw(printName, convertedTextToByte);
            }
            catch(Exception ex)
            {
                Console.WriteLine("Error de emitir o text", ex);
            }
        }

        public void Feed(dynamic input)
        {
            string printName = (string)input.printName;
            try
            {
                RawPrinterHelper.SendRaw(printName, feed);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error Dll feed:" + ex);
            }
        }

        public void Cut(dynamic input)
        {
            string printName = (string)(input.printName);
            try
            {
                RawPrinterHelper.SendRaw(printName, cut);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error Dll Cut" + ex);
            }
        }

        public void QrCode(dynamic input)
        {
            var dict = (IDictionary<string, object>)input;
            string printName = (string)(input.printName);
            string qrCode = (string)input.qrCode;
            int widthQrCode = dict.ContainsKey("widthQrCode") ? (int)input.widthQrCode : 8;
            byte[] qrBytes =GerarQrCodeEscPos(qrCode, tamanhoModulo: widthQrCode, nivelCorrecao: 0x31);

            try
            {
                RawPrinterHelper.SendRaw(printName, qrBytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error Dll Cut" + ex);
            }
        }

         public void Image(dynamic input)
        {
            try
            {
                string image = (string)(input.image);
                string printName = (string)(input.printName);
                byte[] imageBytes = Base64ParaEscPos(image);
                RawPrinterHelper.SendRaw(printName, imageBytes);
            }catch(Exception ex)
            {
                Console.WriteLine("Error Dll Image" + ex);
            }
        }


    }
}
