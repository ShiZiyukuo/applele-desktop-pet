using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Buffers;

namespace Applele.Windows.Animation;

internal static class FrameMixer
{
    public static byte[] Capture(Bitmap bitmap)
    {
        var data=bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
        try{var pixels=new byte[data.Stride*bitmap.Height];Marshal.Copy(data.Scan0,pixels,0,pixels.Length);return pixels;}
        finally{bitmap.UnlockBits(data);}
    }
    public static void Mix(Bitmap target,byte[] source,double amount)
    {
        if(amount>=1)return;
        var data=target.LockBits(new Rectangle(0,0,target.Width,target.Height),ImageLockMode.ReadWrite,PixelFormat.Format32bppPArgb);
        try
        {
            var length=data.Stride*target.Height;
            if(source.Length!=length)throw new ArgumentException("Animation surfaces must have the same dimensions.",nameof(source));
            var pixels=ArrayPool<byte>.Shared.Rent(length);
            try
            {
                Marshal.Copy(data.Scan0,pixels,0,length);var a=(int)Math.Clamp(Math.Round(amount*256),0,256);
                BlendPixels(source,pixels,length,a);
                Marshal.Copy(pixels,0,data.Scan0,length);
            }
            finally{ArrayPool<byte>.Shared.Return(pixels);}
        }
        finally{target.UnlockBits(data);}
    }
    public static void Blend(Bitmap target,Bitmap first,Bitmap second,double amount)
    {
        if(target.Size!=first.Size || target.Size!=second.Size || ReferenceEquals(target,first) || ReferenceEquals(target,second))
            throw new ArgumentException("Blend requires distinct, equally sized target and source surfaces.");
        var length=target.Width*target.Height*4;
        var a=ArrayPool<byte>.Shared.Rent(length);var b=ArrayPool<byte>.Shared.Rent(length);
        try
        {
            Read(first,a);Read(second,b);BlendPixels(a,b,length,(int)Math.Clamp(Math.Round(amount*256),0,256));
            var data=target.LockBits(new Rectangle(Point.Empty,target.Size),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
            try{Marshal.Copy(b,0,data.Scan0,length);}finally{target.UnlockBits(data);}
        }
        finally{ArrayPool<byte>.Shared.Return(a);ArrayPool<byte>.Shared.Return(b);}
    }
    static void Read(Bitmap source,byte[] pixels)
    {
        var data=source.LockBits(new Rectangle(Point.Empty,source.Size),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
        try{Marshal.Copy(data.Scan0,pixels,0,source.Width*source.Height*4);}finally{source.UnlockBits(data);}
    }
    static void BlendPixels(byte[] source,byte[] target,int length,int amount)
    {
        for(int i=0;i<length;i++)target[i]=(byte)((source[i]*(256-amount)+target[i]*amount+128)>>8);
    }
}
