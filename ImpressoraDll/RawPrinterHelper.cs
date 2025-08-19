using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ImpressoraDll
{
    internal class RawPrinterHelper
    {
            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
            public class DOCINFOA
            {
                [MarshalAs(UnmanagedType.LPStr)]
                public string pDocName;
                [MarshalAs(UnmanagedType.LPStr)]
                public string pOutputFile;
                [MarshalAs(UnmanagedType.LPStr)]
                public string pDataType;
                internal string pDatatype;
            }

            [DllImport("winspool.Drv", EntryPoint = "OpenPrinterA",
                SetLastError = true, CharSet = CharSet.Ansi,
                ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
            public static extern bool OpenPrinter(string src, out IntPtr hPrinter, IntPtr pd);

            [DllImport("winspool.Drv", EntryPoint = "ClosePrinter",
                SetLastError = true, ExactSpelling = true,
                CallingConvention = CallingConvention.StdCall)]
            public static extern bool ClosePrinter(IntPtr hPrinter);

            [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterA",
                SetLastError = true, CharSet = CharSet.Ansi,
                ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
            public static extern bool StartDocPrinter(IntPtr hPrinter, int level, [In] DOCINFOA di);

            [DllImport("winspool.Drv", EntryPoint = "EndDocPrinter",
                SetLastError = true, ExactSpelling = true,
                CallingConvention = CallingConvention.StdCall)]
            public static extern bool EndDocPrinter(IntPtr hPrinter);

            [DllImport("winspool.Drv", EntryPoint = "StartPagePrinter",
                SetLastError = true, ExactSpelling = true,
                CallingConvention = CallingConvention.StdCall)]
            public static extern bool StartPagePrinter(IntPtr hPrinter);

            [DllImport("winspool.Drv", EntryPoint = "EndPagePrinter",
                SetLastError = true, ExactSpelling = true,
                CallingConvention = CallingConvention.StdCall)]
            public static extern bool EndPagePrinter(IntPtr hPrinter);

            [DllImport("winspool.Drv", EntryPoint = "WritePrinter",
                SetLastError = true, ExactSpelling = true,
                CallingConvention = CallingConvention.StdCall)]
            public static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

            //public static bool SendBytesToPrinter(string printerName, IntPtr pBytes, int count)
            //{

            //    Debug.WriteLine("SEND BYTE TO PRINT" + printerName + pBytes + count);
            //    IntPtr hPrinter;
            //    DOCINFOA di = new DOCINFOA
            //    {
            //        pDocName = "RAW Document",
            //        pDataType = "RAW"
            //    };

            //    if (OpenPrinter(printerName, out hPrinter, IntPtr.Zero))
            //    {
            //        Debug.WriteLine("OPA BATI AQUI NO OPEN PRINTER");
            //        if (StartDocPrinter(hPrinter, 1, di))
            //        {
            //            if (StartPagePrinter(hPrinter))
            //            {
            //                Debug.WriteLine("OPA");
            //                WritePrinter(hPrinter, pBytes, count, out _);
            //                Debug.WriteLine("OPA" + pBytes);

            //                EndPagePrinter(hPrinter);


            //            }
            //            Debug.WriteLine("OPA KAKAKA");
            //            EndDocPrinter(hPrinter);
            //        }
            //        ClosePrinter(hPrinter);
            //        return true;
            //    }
            //    return false;
            //}


            public static bool SendRaw(string printerName, byte[] bytes)
            {
                IntPtr hPrinter;
                if (!OpenPrinter(printerName, out hPrinter, IntPtr.Zero))
                    return false;

                try
                {
                    var di = new DOCINFOA
                    {
                        pDocName = "ESC/POS Cut",
                        pDatatype = "RAW"
                    };

                    if (!StartDocPrinter(hPrinter, 1, di)) return false;
                    try
                    {
                        if (!StartPagePrinter(hPrinter)) return false;
                        try
                        {
                            IntPtr unmanagedPointer = Marshal.AllocHGlobal(bytes.Length);
                            try
                            {
                                Marshal.Copy(bytes, 0, unmanagedPointer, bytes.Length);
                                return WritePrinter(hPrinter, unmanagedPointer, bytes.Length, out _);
                            }
                            finally
                            {
                                Marshal.FreeHGlobal(unmanagedPointer);
                            }
                        }
                        finally
                        {
                            EndPagePrinter(hPrinter);
                        }
                    }
                    finally
                    {
                        EndDocPrinter(hPrinter);
                    }
                }
                finally
                {
                    ClosePrinter(hPrinter);
                }
            }
        }
    }
