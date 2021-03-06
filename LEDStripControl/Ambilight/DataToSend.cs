﻿using System;
using System.Drawing;
using System.Drawing.Imaging;

public sealed class DataToSend : IDisposable
{
    public object Lock { get; } = new object();

    public AreaForLED[] Data { get; private set; }
    public AreaForLED[] CornerData { get; private set; }

    private int area_height, area_width;

    ScreenCapture screen_capture;

    // характеристики
    private ScreenSize screen = new ScreenSize();
    private int num_vertical_leds, num_horizontal_leds;

    public int NumVerticalLeds => num_vertical_leds;
    public int NumHorizontalLeds => num_horizontal_leds;

    public DataToSend(int num_vertical_leds, int num_horizontal_leds)
    {
        this.num_vertical_leds = num_vertical_leds;
        this.num_horizontal_leds = num_horizontal_leds;
        Data = new AreaForLED[2 * num_vertical_leds + 2 * num_horizontal_leds];
        CornerData = new AreaForLED[4];
        screen_capture = new ScreenCapture(0, 0);
        screen.Width = screen_capture.Width;
        screen.Height = screen_capture.Height;
        FillData();
    }

    private void FillData()
    {
        area_height = screen.Height / (num_vertical_leds + 2);
        area_width = screen.Width / (num_horizontal_leds + 2);

        int margin_top = (screen.Height - area_height * (num_vertical_leds + 2)) / 2;
        int margin_left = (screen.Width - area_width * (num_horizontal_leds + 2)) / 2;

        // вертикальные полосы
        for (int i = 1; i <= num_vertical_leds; ++i)
        {
            Data[i - 1] = new AreaForLED(margin_left, screen.Height - (i + 1) * area_height - margin_top, area_width, area_height);
            Data[2 * num_vertical_leds + num_horizontal_leds - i]
                = new AreaForLED(screen.Width - area_width - margin_left, screen.Height - (i + 1) * area_height - margin_top, area_width, area_height);
        }

        // горизонтальные полосы
        for (int i = 1; i <= num_horizontal_leds; ++i)
        {
            Data[num_vertical_leds + i - 1] = new AreaForLED(i * area_width + margin_left, margin_top, area_width, area_height);
            Data[2 * num_vertical_leds + 2 * num_horizontal_leds - i]
                = new AreaForLED(i * area_width + margin_left, screen.Height - area_height - margin_top, area_width, area_height);
        }

        // левый верхний угол
        CornerData[0] = new AreaForLED(margin_left, margin_top, area_width, area_height);
        // правый верхний угол
        CornerData[1] = new AreaForLED(screen.Width - area_width - margin_left, margin_top, area_width, area_height);
        // правый нижний угол
        CornerData[2] = new AreaForLED(screen.Width - area_width - margin_left, screen.Height - area_height - margin_top, area_width, area_height);
        // правый левый угол
        CornerData[3] = new AreaForLED(margin_left, screen.Height - area_height - margin_top, area_width, area_height);
    }

    public void RefreshSettings(int num_vertical_leds, int num_horizontal_leds)
    {
        this.num_vertical_leds = num_vertical_leds;
        this.num_horizontal_leds = num_horizontal_leds;
        screen_capture.RefreshScreenResolution();
        screen.Width = screen_capture.Width;
        screen.Height = screen_capture.Height;
        Data = new AreaForLED[2 * num_vertical_leds + 2 * num_horizontal_leds];
        FillData();
    }

    /* код взял с:
    https://github.com/fabsenet/adrilight/blob/master/adrilight/DesktopDuplication/DesktopDuplicatorReader.cs
    метод GetAverageColorOfRectangularRegion */
    private unsafe void SetAverageColor(Rectangle spotRectangle, int stepy, int stepx, BitmapData bitmapData,
        out int sumR, out int sumG, out int sumB, out int count)
    {
        sumR = 0;
        sumG = 0;
        sumB = 0;
        count = 0;

        var stepCount = spotRectangle.Width / stepx;
        var stepxTimes4 = stepx * 4;
        for (var y = spotRectangle.Top; y < spotRectangle.Bottom; y += stepy)
        {
            byte* pointer = (byte*)bitmapData.Scan0 + bitmapData.Stride * y + 4 * spotRectangle.Left;
            for (int i = 0; i < stepCount; i++)
            {
                sumR += pointer[2];
                sumG += pointer[1];
                sumB += pointer[0];

                pointer += stepxTimes4;
            }
            count += stepCount;
        }
    }

    public void RefreshData()
    {
        BitmapData bitmapData = new BitmapData();
        Bitmap image = screen_capture.GetFrame();
        if (image == null) return;
        const int numberOfSteps = 15;
        int stepx = Math.Max(1, area_width / numberOfSteps);
        int stepy = Math.Max(1, area_height / numberOfSteps);
        image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb, bitmapData);
        lock (Lock)
        {
            for (int i = 0; i < Data.Length; ++i)
            {
                SetAverageColor(Data[i].Rectangle, stepy, stepx, bitmapData, out int R, out int G, out int B, out int count);
                if (R < 2 && G < 2 && B < 2) R = G = B = 5;
                Data[i].SetRGB((byte)(R / count), (byte)(G / count), (byte)(B / count));
            }
            int r, g, b, _count;
            SetAverageColor(CornerData[0].Rectangle, stepy, stepx, bitmapData, out r, out g, out b, out _count);
            if (r < 2 && g < 2 && b < 2) r = g = b = 5;
            CornerData[0].SetRGB((byte)(r / _count), (byte)(g / _count), (byte)(b / _count));
            SetAverageColor(CornerData[1].Rectangle, stepy, stepx, bitmapData, out r, out g, out b, out _count);
            if (r < 2 && g < 2 && b < 2) r = g = b = 5;
            CornerData[1].SetRGB((byte)(r / _count), (byte)(g / _count), (byte)(b / _count));
            SetAverageColor(CornerData[2].Rectangle, stepy, stepx, bitmapData, out r, out g, out b, out _count);
            if (r < 2 && g < 2 && b < 2) r = g = b = 5;
            CornerData[2].SetRGB((byte)(r / _count), (byte)(g / _count), (byte)(b / _count));
            SetAverageColor(CornerData[3].Rectangle, stepy, stepx, bitmapData, out r, out g, out b, out _count);
            if (r < 2 && g < 2 && b < 2) r = g = b = 5;
            CornerData[3].SetRGB((byte)(r / _count), (byte)(g / _count), (byte)(b / _count));
        }
        image.UnlockBits(bitmapData);
    }

    public void Dispose() => screen_capture.Dispose();
}