using System.Collections.Generic;
using System.Drawing;
using BizHawk.Client.EmuHawk.FilterManager;

using BizHawk.Bizware.BizwareGL;
using OpenTK;

// Here's how to make a filter:
// 1. Reset your state entirely in Initialize(). 
//    The filter will be re-initialized several times while the chain is getting worked out, but not re-instantiated. 
//    This is sort of annoying, but there's pretty good reasons for it (some external process has created the filters and set parameters needed to govern their chaining and surface properties)
// 2. In Initialize(), be sure to use DeclareInput
// (something about PresizeInput())
// 3. PresizeOutput() will be called next
// 4. In SetInputFormat(), use DeclareOutput to set the output based on your desires, or the provided input format.
// 5. In Run(), the render target is already set. If using a texture, use InputTexture
// 6. In Run(), if supplying an output texture, use YieldOutput
namespace BizHawk.Client.EmuHawk.Filters
{
	public class BaseFilter
	{
		// initialization stuff
		public void BeginInitialization(FilterProgram program) { _ioSurfaceInfos.Clear(); FilterProgram = program; }
		public virtual void Initialize() { }
		public virtual Size PresizeInput(string channel, Size size) => size;
		public virtual Size PresizeOutput(string channel, Size size) => size;
		public virtual void SetInputFormat(string channel, SurfaceState state) { } //TODO - why a different param order than DeclareOutput?
		public Dictionary<string, object> Parameters = new Dictionary<string, object>();
		public bool IsNop { get; set; }

		// runtime signals
		public virtual Vector2 UntransformPoint(string channel, Vector2 point)
		{
			// base class behaviour here just uses the input and output sizes, if appropriate. few filters will have to do anything more complex
			var input = FindInput(channel);
			var output = FindOutput(channel);
			if (input != null && output != null)
			{
				point.X *= ((float)input.SurfaceFormat.Size.Width) / (float)output.SurfaceFormat.Size.Width;
				point.Y *= ((float)input.SurfaceFormat.Size.Height) / (float)output.SurfaceFormat.Size.Height;
			}
			return point;
		}

		public virtual Vector2 TransformPoint(string channel, Vector2 point)
		{
			// base class behaviour here just uses the input and output sizes, if appropriate. few filters will have to do anything more complex
			var input = FindInput(channel);
			var output = FindOutput(channel);
			if (input != null && output != null)
			{
				point.X *= ((float)output.SurfaceFormat.Size.Width) / (float)input.SurfaceFormat.Size.Width;
				point.Y *= ((float)output.SurfaceFormat.Size.Height) / (float)input.SurfaceFormat.Size.Height;
			}
			return point;
		}

		public void SetInput(Texture2d tex)
		{
			InputTexture = tex;
		}
		public virtual void Run() { }
		public Texture2d GetOutput() => _outputTexture;

		// filter actions
		protected void YieldOutput(Texture2d tex)
		{
			_outputTexture = tex;
		}

		protected FilterProgram FilterProgram;
		protected Texture2d InputTexture;
		private Texture2d _outputTexture;

		// setup utilities
		protected IOSurfaceInfo DeclareInput(SurfaceDisposition disposition = SurfaceDisposition.Unspecified, string channel = "default")
		{
			return DeclareIO(SurfaceDirection.Input, channel, disposition);
		}

		protected IOSurfaceInfo DeclareOutput(SurfaceDisposition disposition = SurfaceDisposition.Unspecified, string channel = "default")
		{
			return DeclareIO(SurfaceDirection.Output, channel, disposition);
		}

		// TODO - why a different param order than DeclareOutput?
		protected RenderTarget GetTempTarget(int width, int height)
		{
			return FilterProgram.GetTempTarget(width, height);
		}

		protected IOSurfaceInfo DeclareOutput(SurfaceState state, string channel = "default")
		{
			var iosi = DeclareIO(SurfaceDirection.Output, channel, state.SurfaceDisposition);
			iosi.SurfaceFormat = state.SurfaceFormat;
			return iosi;
		}

		public IOSurfaceInfo FindInput(string channel = "default")
		{
			return FindIOSurfaceInfo(channel, SurfaceDirection.Input);
		}

		public IOSurfaceInfo FindOutput(string channel = "default")
		{
			return FindIOSurfaceInfo(channel, SurfaceDirection.Output);
		}

		private IOSurfaceInfo DeclareIO(SurfaceDirection direction, string channel, SurfaceDisposition disposition)
		{
			var iosi = new IOSurfaceInfo
			{
				SurfaceDirection = direction,
				Channel = channel,
				SurfaceDisposition = disposition
			};

			_ioSurfaceInfos.Add(iosi);
			return iosi;
		}

		private readonly List<IOSurfaceInfo> _ioSurfaceInfos = new List<IOSurfaceInfo>();


		private IOSurfaceInfo FindIOSurfaceInfo(string channel, SurfaceDirection direction)
		{
			foreach (var iosi in _ioSurfaceInfos)
			{
				if (iosi.Channel == channel && iosi.SurfaceDirection == direction)
				{
					return iosi;
				}
			}

			return null;
		}

		public class IOSurfaceInfo
		{
			public SurfaceFormat SurfaceFormat { get; set; }
			public SurfaceDirection SurfaceDirection { get; set; }
			public SurfaceDisposition SurfaceDisposition { get; set; }
			public string Channel { get; set; }
		}

		public enum SurfaceDirection
		{
			Input, Output
		}
	}
}