﻿using BizHawk.Common;
using BizHawk.Emulation.Common;

namespace BizHawk.Emulation.Cores.Consoles.ChannelF
{
	public partial class ChannelF : IVideoProvider, IRegionable
	{
		/// <summary>
		/// 128x64 pixels - 8192x2bits (2 KB)
		/// For the purposes of this core we will use 8192 bytes and just mask 0x03
		/// (Also adding an additional 10 rows to the RAM buffer so that it's more aligned with the actual display)
		/// </summary>
		public byte[] VRAM = new byte[128 * 64];


		public static readonly int[] FPalette =
		{
			//0x101010, 0xFDFDFD, 0x5331FF, 0x5DCC02, 0xF33F4B, 0xE0E0E0, 0xA6FF91, 0xD0CEFF
			
			Colors.ARGB(0x10, 0x10, 0x10),		// Black
			Colors.ARGB(0xFD, 0xFD, 0xFD),		// White
			Colors.ARGB(0xFF, 0x31, 0x53),		// Red
			Colors.ARGB(0x02, 0xCC, 0x5D),		// Green
			Colors.ARGB(0x4B, 0x3F, 0xF3),		// Blue
			Colors.ARGB(0xE0, 0xE0, 0xE0),		// Gray
			Colors.ARGB(0x91, 0xFF, 0xA6),		// BGreen
			Colors.ARGB(0xCE, 0xD0, 0xFF),		// BBlue			
		};

		public static readonly int[] CMap =
		{
			0, 1, 1, 1,
			7, 4, 2, 3,
			5, 4, 2, 3,
			6, 4, 2, 3,
		};

		private int _latch_colour = 2;
		private int _latch_x;
		private int _latch_y;
		private int[] videoBuffer;
		private double _pixelClockCounter;
		private double _pixelClocksRemaining;

		
		private int ScanlineRepeats;
		private int PixelWidth;
		private int HTotal;
		private int HBlankOff;
		private int HBlankOn;
		private int VTotal;
		private int VBlankOff;
		private int VBlankOn;
		private double PixelClocksPerCpuClock;
		private double PixelClocksPerFrame;

		public void SetupVideo()
		{
			videoBuffer = new int[HTotal * VTotal];
		}

		/// <summary>
		/// Called after every CPU clock
		/// </summary>
		private void ClockVideo()
		{			
			while (_pixelClocksRemaining > 1)
			{
				var currScanline = (int)(_pixelClockCounter / HTotal);
				var currPixelInLine = (int)(_pixelClockCounter % HTotal);
				var currRowInVram = currScanline / ScanlineRepeats;
				var currColInVram = currPixelInLine / PixelWidth;

				if (currScanline < VBlankOff || currScanline >= VBlankOn)
				{
					// vertical flyback
				}
				else if (currPixelInLine < HBlankOff || currPixelInLine >= HBlankOn)
				{
					// horizontal flyback
				}
				else
				{
					// active display
					if (currRowInVram < 64)
					{
						var p1 = (VRAM[(currRowInVram * 0x80) + 125]) & 0x03;
						var p2 = (VRAM[(currRowInVram * 0x80) + 126]) & 0x03;
						var pOffset = ((p2 & 0x02) | (p1 >> 1)) << 2;

						var colourIndex = pOffset + (VRAM[currColInVram | (currRowInVram << 7)] & 0x03);
						videoBuffer[(currScanline * HTotal) + currPixelInLine] = FPalette[CMap[colourIndex]];
					}
				}

				_pixelClockCounter++;
				_pixelClocksRemaining -= 1;				
			}

			_pixelClocksRemaining += PixelClocksPerCpuClock;
			_pixelClockCounter %= PixelClocksPerFrame;
		}

		private int HDisplayable => HBlankOn - HBlankOff;
		private int VDisplayable => VBlankOn - VBlankOff;

		private int[] ClampBuffer(int[] buffer, int originalWidth, int originalHeight, int trimLeft, int trimTop, int trimRight, int trimBottom)
		{
			int newWidth = originalWidth - trimLeft - trimRight;
			int newHeight = originalHeight - trimTop - trimBottom;
			int[] newBuffer = new int[newWidth * newHeight];

			for (int y = 0; y < newHeight; y++)
			{
				for (int x = 0; x < newWidth; x++)
				{
					int originalIndex = (y + trimTop) * originalWidth + (x + trimLeft);
					int newIndex = y * newWidth + x;
					newBuffer[newIndex] = buffer[originalIndex];
				}
			}

			return newBuffer;
		}

		private static double GetVerticalModifier(int bufferWidth, int bufferHeight, double targetAspectRatio)
		{
			// Calculate the current aspect ratio
			double currentAspectRatio = (double)bufferWidth / bufferHeight;

			// Calculate the vertical modifier needed to achieve the target aspect ratio
			double verticalModifier = currentAspectRatio / targetAspectRatio;

			return verticalModifier;
		}

		public int VirtualWidth => HDisplayable * 2;
		public int VirtualHeight => (int)(VDisplayable * GetVerticalModifier(HDisplayable, VDisplayable, 4.0/3.0)) * 2;
		public int BufferWidth => HDisplayable;
		public int BufferHeight => VDisplayable;
		public int BackgroundColor => Colors.ARGB(0xFF, 0xFF, 0xFF);
		public int VsyncNumerator { get; private set; }
		public int VsyncDenominator { get; private set; }


		public int[] GetVideoBuffer()
		{
			// https://channelf.se/veswiki/index.php?title=VRAM
			// 'The emulator MESS uses a fixed 102x58 resolution starting at (4,4) but the safe area for a real system is about 95x58 pixels'
			// 'Note that the pixel aspect is a lot closer to widescreen (16:9) than standard definition (4:3). On a TV from the 70's or 80's pixels are rectangular, standing up. In widescreen mode they are close to perfect squares'
			// https://channelf.se/veswiki/index.php?title=Resolution
			// 'Even though PAL televisions system has more lines vertically, the Channel F displays about the same as on the original NTSC video system'
			//
			// Right now we are just trimming based on the HBLANK and VBLANK values (we might need to go further like the other emulators)
			// VirtualWidth is being used to force the aspect ratio into 4:3
			// On real hardware it looks like this (so we are close): https://www.youtube.com/watch?v=ZvQA9tiEIuQ
			return ClampBuffer(videoBuffer, HTotal, VTotal, HBlankOff, VBlankOff, HTotal - HBlankOn, VTotal - VBlankOn);
		}	

		public DisplayType Region => region == RegionType.NTSC ? DisplayType.NTSC : DisplayType.PAL;

		/*
		private void BuildFrameFromRAM()
		{
			for (int r = 0; r < 64; r++)
			{
				// lines
				var p1 = (VRAM[(r * 0x80) + 125]) & 0x03;
				var p2 = (VRAM[(r * 0x80) + 126]) & 0x03;
				var pOffset = ((p2 & 0x02) | (p1 >> 1)) << 2;

				for (int c = 0; c < 128; c++)
				{
					// columns
					var colourIndex = pOffset + (VRAM[c | (r << 7)] & 0x03);
					frameBuffer[(r << 7) + c] = CMap[colourIndex];
				}
			}
		}
		*/
	}
}
