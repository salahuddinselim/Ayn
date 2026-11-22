using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Activity;
using WindowsBuddy.BuddyCapsule.Animation;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Core;
using WindowsBuddy.Services.Pet;
using WindowsBuddy.Tools.FileShelf;

namespace WindowsBuddy.BuddyCapsule.Renderer;

public sealed class GardenView : PetView
{
	private sealed class TextVisual : DrawingVisual
	{
		public TextVisual()
		{
			base.VisualTextRenderingMode = TextRenderingMode.Grayscale;
			base.VisualTextHintingMode = TextHintingMode.Fixed;
		}
	}

	private sealed class CachedFrame
	{
		public BitmapSource Image;

		public BitmapSource Props;

		public DrawingGroup BodyDrawing;

		public DrawingGroup PropDrawing;

		public Rect Bounds;

		public Rect PropBounds;
	}

	private static class SceneAtlas
	{
		private sealed class Part
		{
			public Rect Bounds;

			public long Offset;

			public int Length;
		}

		private sealed class Entry
		{
			public double X;

			public Part Body;

			public Part Props;
		}

		private sealed class Clip
		{
			public long Offset;

			public int Count;
		}

		private sealed class Surface
		{
			public WriteableBitmap Image;

			public byte[] Pixels;

			public byte[] Next;

			public long Offset = -1L;

			public Surface(int w, int h)
			{
				Image = new WriteableBitmap(w, h, 192.0, 192.0, PixelFormats.Pbgra32, null);
				Pixels = new byte[w * h * 4];
				Next = new byte[w * h * 4];
			}
		}

		private static readonly Dictionary<string, Clip> entries = new Dictionary<string, Clip>();

		private static string activeKey;

		public static bool LegacyAtlas;

		private static Entry[] activeFrames;

		private static BinaryReader reader;

		private static bool opened;

		private static byte[] decodeBuffer = new byte[0];

		private static byte[] compressedBuffer = new byte[0];

		private static PetFrameInflater inflater;

		private static Surface bodySurface;

		private static Surface propSurface;

		public static string Key(GardenScene scene, Dictionary<string, double> held, bool headphones)
		{
			if (scene == null || scene.Variant != null || scene.Name == "chalk" || !LegacyAtlas)
			{
				return null;
			}
			if (GardenScenes.Stories.Contains(scene.Name) && scene.Duration >= 8.0)
			{
				return "story:" + scene.Name + ((scene.Name == "rain" && Math.Abs(scene.At("x", 0.0, 140.0) - 140.0) > 1.0) ? "-walk" : "");
			}
			if (scene.Name == "dance" && headphones && scene.Duration < 3.0)
			{
				return "music";
			}
			if (new string[6] { "look-and-shift", "scratch", "stretch", "sit", "small-walk", "wink" }.Contains(scene.Name))
			{
				held.TryGetValue("sit", out var value);
				bool flag = Math.Abs(scene.At("x", scene.Duration, 140.0) - scene.At("x", 0.0, 140.0)) > 0.01;
				return "micro:" + scene.Name + ":" + ((value > 0.5) ? 1 : 0) + ":" + ((scene.At("sit", scene.Duration, 0.0) > 0.5) ? 1 : 0) + ":" + (flag ? 1 : 0);
			}
			return null;
		}

		private static Part ReadPart()
		{
			Part part = new Part();
			part.Bounds = new Rect(reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble());
			part.Length = reader.ReadInt32();
			Part part2 = part;
			if (part2.Length < 0)
			{
				part2.Length = -part2.Length;
				part2.Offset = reader.ReadInt64();
			}
			else
			{
				part2.Offset = reader.BaseStream.Position;
				reader.BaseStream.Seek(part2.Length, SeekOrigin.Current);
			}
			return part2;
		}

		private static void Open()
		{
			if (opened)
			{
				return;
			}
			opened = true;
			Stream manifestResourceStream = typeof(GardenView).Assembly.GetManifestResourceStream("Ayn.Pet.scene-flipbooks");
			if (manifestResourceStream == null)
			{
				return;
			}
			reader = new BinaryReader(manifestResourceStream);
			if (reader.ReadString() != "VAYPET31")
			{
				throw new InvalidDataException("Pet flipbook version");
			}
			int num = reader.ReadInt32();
			for (int i = 0; i < num; i++)
			{
				string key = reader.ReadString();
				int num2 = reader.ReadInt32();
				entries[key] = new Clip
				{
					Offset = reader.BaseStream.Position,
					Count = num2
				};
				for (int j = 0; j < num2; j++)
				{
					reader.BaseStream.Seek(8L, SeekOrigin.Current);
					SkipPart();
					SkipPart();
				}
			}
		}

		private static void SkipPart()
		{
			reader.BaseStream.Seek(32L, SeekOrigin.Current);
			int num = reader.ReadInt32();
			reader.BaseStream.Seek((num < 0) ? 8 : num, SeekOrigin.Current);
		}

		public static void Release()
		{
			activeFrames = null;
			activeKey = null;
			entries.Clear();
			bodySurface = (propSurface = null);
			decodeBuffer = (compressedBuffer = new byte[0]);
			inflater = null;
			if (reader != null)
			{
				reader.Dispose();
				reader = null;
			}
			opened = false;
		}

		private static Surface MakeSurface(bool props)
		{
			int num = 1;
			int num2 = 1;
			Entry[] array = activeFrames;
			foreach (Entry entry in array)
			{
				Part part = (props ? entry.Props : entry.Body);
				if (part.Length != 0)
				{
					num = Math.Max(num, (int)Math.Round(part.Bounds.Width * 2.0));
					num2 = Math.Max(num2, (int)Math.Round(part.Bounds.Height * 2.0));
				}
			}
			return new Surface(num, num2);
		}

		private static BitmapSource Decode(Part part, Surface surface)
		{
			if (part.Length == 0)
			{
				return null;
			}
			if (surface.Offset == part.Offset)
			{
				return surface.Image;
			}
			reader.BaseStream.Position = part.Offset;
			int num = reader.ReadInt32();
			int num2 = reader.ReadInt32();
			int num3 = checked(num * num2 * 4);
			if (decodeBuffer.Length < num3)
			{
				decodeBuffer = new byte[num3];
			}
			int num4 = part.Length - 8;
			if (compressedBuffer.Length < num4)
			{
				compressedBuffer = new byte[num4];
			}
			int num5;
			for (int i = 0; i < num4; i += num5)
			{
				num5 = reader.Read(compressedBuffer, i, num4 - i);
				if (num5 == 0)
				{
					throw new EndOfStreamException("Pet frame pixels");
				}
			}
			if (inflater == null)
			{
				inflater = new PetFrameInflater();
			}
			inflater.Decode(compressedBuffer, num4, decodeBuffer, num3);
			int num6 = surface.Image.PixelWidth * 4;
			Array.Clear(surface.Next, 0, surface.Next.Length);
			for (int j = 0; j < num2; j++)
			{
				Buffer.BlockCopy(decodeBuffer, j * num * 4, surface.Next, j * num6, num * 4);
			}
			int num7 = surface.Image.PixelWidth;
			int num8 = -1;
			int num9 = surface.Image.PixelHeight;
			int num10 = -1;
			for (int k = 0; k < surface.Image.PixelHeight; k++)
			{
				for (int l = 0; l < surface.Image.PixelWidth; l++)
				{
					int num11 = k * num6 + l * 4;
					bool flag = false;
					for (int m = 0; m < 4; m++)
					{
						if (surface.Pixels[num11 + m] != surface.Next[num11 + m])
						{
							flag = true;
						}
					}
					if (flag)
					{
						num7 = Math.Min(num7, l);
						num8 = Math.Max(num8, l);
						num9 = Math.Min(num9, k);
						num10 = Math.Max(num10, k);
					}
				}
			}
			if (num8 >= num7)
			{
				surface.Image.WritePixels(new Int32Rect(num7, num9, num8 - num7 + 1, num10 - num9 + 1), surface.Next, num6, num9 * num6 + num7 * 4);
			}
			byte[] pixels = surface.Pixels;
			surface.Pixels = surface.Next;
			surface.Next = pixels;
			surface.Offset = part.Offset;
			return surface.Image;
		}

		public static CachedFrame Get(string key, int index, double x)
		{
			if (key == null)
			{
				return null;
			}
			Open();
			if (!entries.TryGetValue(key, out var value) || index < 0 || index >= value.Count)
			{
				return null;
			}
			if (activeKey != key)
			{
				activeKey = key;
				reader.BaseStream.Position = value.Offset;
				activeFrames = new Entry[value.Count];
				for (int i = 0; i < value.Count; i++)
				{
					activeFrames[i] = new Entry
					{
						X = reader.ReadDouble(),
						Body = ReadPart(),
						Props = ReadPart()
					};
				}
				bodySurface = MakeSurface(props: false);
				propSurface = MakeSurface(props: true);
			}
			Entry entry = activeFrames[index];
			Rect bounds = entry.Body.Bounds;
			bounds.Offset(x - entry.X, 0.0);
			CachedSceneFrames++;
			BitmapSource bitmapSource = Decode(entry.Body, bodySurface);
			BitmapSource bitmapSource2 = Decode(entry.Props, propSurface);
			bounds.Width = (double)bitmapSource.PixelWidth / 2.0;
			bounds.Height = (double)bitmapSource.PixelHeight / 2.0;
			Rect bounds2 = entry.Props.Bounds;
			if (bitmapSource2 != null)
			{
				bounds2.Width = (double)bitmapSource2.PixelWidth / 2.0;
				bounds2.Height = (double)bitmapSource2.PixelHeight / 2.0;
			}
			CachedFrame cachedFrame = new CachedFrame();
			cachedFrame.Image = bitmapSource;
			cachedFrame.Bounds = bounds;
			cachedFrame.Props = bitmapSource2;
			cachedFrame.PropBounds = bounds2;
			return cachedFrame;
		}
	}

	internal const double BigWaitSeconds = 300.0;

	public const double Side = 78.0;

	public const double PropAt = 184.0;

	public const double RobotScale = 1.12;

	public const double Ground = 38.5;

	internal const double MovingIntervalOnBattery = 33.3334;

	private const double G = 38.5;

	private static readonly Random random = new Random();

	private static readonly Brush shell = Paint("#F4F1EA");

	private static readonly Brush shade = Paint("#CCC8C1");

	private static readonly Brush ink = Paint("#101114");

	private static readonly Brush cyan = Paint("#69C9E8");

	private static readonly Brush wood = Paint("#C79868");

	private readonly DispatcherTimer director = new DispatcherTimer(DispatcherPriority.Background);

	private readonly Dictionary<string, double> held = new Dictionary<string, double>();

	private PetVectorRig rig;

	private GardenScene scene;

	private GardenScene reaction;

	private double reactionElapsed;

	private double elapsed;

	private double lastTick;

	private double x = -32.0;

	private double previousX = -32.0;

	private double step;

	private PetMoment moment;

	private FormattedText bubbleText;

	private FormattedText primaryText;

	private FormattedText secondaryText;

	private FormattedText chalkText;

	private DrawingGroup bubbleDrawing;

	private string bubbleLayoutKey;

	private Rect bubbleBounds;

	private Rect primaryBounds;

	private Rect secondaryBounds;

	private bool answered;

	private string previousStory = "";

	private string activityLevel = "";

	private int eventIndex;

	private int wake;

	private bool attached;

	private bool onStage;

	private bool departed;

	private bool headphones;

	private bool pillow;

	private Action afterExit;

	private DateTime nextStory = DateTime.MinValue;

	private PetSegment segment;

	private PetSegment pendingBig;

	private PetSegment bigScene;

	private double segmentEnd;

	private double pendingSince;

	private bool bigCommitted;

	private PetActivity activity;

	private PetActivity incoming;

	private readonly Dictionary<string, PetActivity> aftermaths = new Dictionary<string, PetActivity>();

	private string wearKey;

	private string weather;

	private string festival;

	private bool lastMusic;

	private bool musicChanged;

	private bool introBox;

	private string doneFor;

	private double notchOpacity = 1.0;

	private double smL = -15.0;

	private double smR = 15.0;

	private double smT;

	private double smS;

	private double smTime;

	private bool smReady;

	private bool smoothing;

	private double unit = 1.0;

	private double kx = 1.0;

	private static readonly Dictionary<string, double> workX = new Dictionary<string, double>
	{
		{ "book", 128.0 },
		{ "laptop", 122.0 },
		{ "chai", 120.0 },
		{ "headphones", 118.0 },
		{ "rod", 104.0 },
		{ "telescope", 112.0 },
		{ "pillow", 120.0 },
		{ "gadget", 110.0 },
		{ "can", 108.0 },
		{ "notebook", 130.0 },
		{ "popcorn", 124.0 },
		{ "easel", 108.0 },
		{ "cube", 136.0 },
		{ "yarn", 118.0 },
		{ "mat", 116.0 },
		{ "paper", 136.0 },
		{ "table", 112.0 },
		{ "plane", 120.0 },
		{ "umbrella", 96.0 },
		{ "board", 122.0 },
		{ "ball", 124.0 },
		{ "bucket", 126.0 },
		{ "kite", 92.0 },
		{ "bat", 96.0 },
		{ "guitar", 124.0 },
		{ "wand", 100.0 },
		{ "hat", 130.0 },
		{ "yoyo", 100.0 },
		{ "bin", 100.0 },
		{ "dominoes", 112.0 }
	};

	private MatrixTransform stageTransform;

	private readonly Stopwatch budgetClock = Stopwatch.StartNew();

	private double nextSceneAt;

	private double sceneStarted = -1.0;

	private bool walked;

	private readonly Dictionary<string, double> ran = new Dictionary<string, double>();

	private DateTime summaryStart = DateTime.UtcNow;

	private DateTime lastAccount = DateTime.MinValue;

	private DateTime activitySince;

	private string loggedActivity;

	private string wearHat;

	private string wearNeck;

	private static readonly Dictionary<Brush, Dictionary<double, Pen>> pens = new Dictionary<Brush, Dictionary<double, Pen>>();

	private static readonly Dictionary<string, Geometry> paths = new Dictionary<string, Geometry>();

	private Geometry clip;

	private Geometry clipSource;

	private readonly DrawingVisual bodyVisual = new DrawingVisual();

	private readonly DrawingVisual propVisual = new DrawingVisual();

	private readonly DrawingVisual eyeVisual = new DrawingVisual();

	private readonly DrawingVisual bubbleVisual = new TextVisual();

	private BitmapSource renderedProps;

	private Rect renderedPropBounds;

	private string renderedBubble;

	private bool visualsAttached;

	private bool baking;

	private bool eyesReady;

	private bool propsWereAnimating;

	private string eyeEffect;

	private CachedFrame lastFrame;

	private CachedFrame renderedFrame;

	private DrawingGroup vectorBody;

	private DrawingGroup vectorProps;

	private string idleBodyKey;

	private double pixelScale = 1.0;

	private PetVectorRig bodyRig;

	private GardenScene propsScene;

	private CachedFrame vectorFrame;

	private static readonly string[] stillPropScenes = new string[7] { "walk", "look-and-shift", "scratch", "stretch", "sit", "small-walk", "wink" };

	private ImageSource bubbleIcon;

	private static ImageSource cameraImage;

	private static readonly Brush board = Paint("#26332D");

	private static readonly Brush red = Paint("#E57373");

	private static readonly Brush yellow = Paint("#FFD54F");

	private static readonly Brush green = Paint("#81C784");

	private static readonly Brush pink = Paint("#F48FB1");

	private static readonly Brush flameBrush = Paint("#FFB74D");

	internal PetLife QaLife;

	internal int QaRocket = -1;

	internal int QaPlant = -1;

	internal int QaStars = -1;

	internal string QaFestival;

	internal string QaWeather;

	internal string QaOutcome;

	internal string QaDrawing;

	internal bool QaMode;

	private static readonly Brush CyanBright = Fill("#A8E6F7");

	private static readonly Brush Glass = Fill("#26FFFFFF");

	private double handX;

	private double handRight = 15.0;

	private double handJump;

	private static readonly string[] fxActions = new string[32]
	{
		"time", "battery", "sneeze", "hum", "whistle", "sigh", "yawn", "clap", "cheer", "thumbs-up",
		"wipe-brow", "hiccup", "dust-off", "polish", "scratch", "shrug", "look-around", "rub-eyes", "salute", "peek",
		"stretch", "hop", "tap", "nod", "kick", "wiggle", "tilt", "double-blink", "look-up", "look-left",
		"look-right", "wave-small"
	};

	private static readonly Brush[] cubeColors = new Brush[9]
	{
		PetArt.Red,
		PetArt.Yellow,
		PetArt.White,
		PetArt.Yellow,
		PetArt.White,
		PetArt.Red,
		PetArt.White,
		PetArt.Red,
		PetArt.Yellow
	};

	private static readonly Dictionary<string, string> signature = new Dictionary<string, string>
	{
		{ "reading", "page" },
		{ "laptop", "type" },
		{ "chai", "sip" },
		{ "headphones", "nod" },
		{ "fishing", "reel" },
		{ "stargazing", "peer" },
		{ "napping", "snore" },
		{ "tinkering", "hammer" },
		{ "gardening", "water" },
		{ "notebook", "write" },
		{ "popcorn", "munch" },
		{ "painting", "brush" },
		{ "puzzle", "twist" },
		{ "knitting", "knit" },
		{ "yoga", "breathe-deep" },
		{ "origami", "fold" },
		{ "table", "sip" },
		{ "plane", "fold" },
		{ "umbrella", "twirl" },
		{ "board", "chalk-tap" },
		{ "ball", "bounce" },
		{ "bucket", "flap" },
		{ "kite", "tug" },
		{ "bat", "swing-bat" },
		{ "guitar", "strum" },
		{ "wand", "blow" },
		{ "hat", "wand" },
		{ "yoyo", "yoyo" },
		{ "bin", "toss" },
		{ "dominoes", "place" }
	};

	private static readonly Dictionary<string, double> signatureU = new Dictionary<string, double>
	{
		{ "reading", 0.3 },
		{ "laptop", 0.6 },
		{ "fishing", 0.3 },
		{ "stargazing", 0.65 },
		{ "tinkering", 0.327 },
		{ "notebook", 0.7 },
		{ "popcorn", 0.4 },
		{ "painting", 0.7 },
		{ "knitting", 0.3 },
		{ "plane", 0.7 },
		{ "ball", 0.12 },
		{ "bat", 0.4 },
		{ "yoyo", 0.25 },
		{ "bin", 0.5 },
		{ "dominoes", 0.6 }
	};

	private static readonly Dictionary<string, double> outcomeOffset = new Dictionary<string, double>
	{
		{ "chalk/erase", 0.8 },
		{ "cricket/six", 0.0 },
		{ "chalk/crumple", 1.4 },
		{ "kite/tangle", 1.6 },
		{ "catch/nothing", 1.2 }
	};

	public Rect BubbleBounds
	{
		get
		{
			EnsureBubbleLayout();
			return bubbleBounds;
		}
	}

	public bool DialogueExpands
	{
		get
		{
			if (moment != null)
			{
				return moment.Suggestion;
			}
			return false;
		}
	}

	public bool HasActionPrompt
	{
		get
		{
			if (moment != null)
			{
				return moment.Suggestion;
			}
			return false;
		}
	}

	public Func<bool> CanPresentDialogue { get; set; }

	private bool DialogueSurfaceReady
	{
		get
		{
			if (CanPresentDialogue != null)
			{
				return CanPresentDialogue();
			}
			return true;
		}
	}

	private bool PromptButtonsReady
	{
		get
		{
			if (HasActionPrompt && !answered && DialogueSurfaceReady && scene != null && scene.Name.StartsWith("moment-", StringComparison.Ordinal) && elapsed >= 2.25 && elapsed <= scene.Duration - 1.75)
			{
				return Math.Abs(Value("bubble.x", -98.0) - 177.0) < 0.01;
			}
			return false;
		}
	}

	public double DialogueHeight
	{
		get
		{
			if (moment == null || string.IsNullOrEmpty(moment.Title))
			{
				return 0.0;
			}
			double num = Math.Max(0.9, Math.Min(1.2, NotchConfig.Instance.SizeScale));
			return Math.Ceiling(BubbleText(moment.Title, 15.0 * num, PixelsPerDip).Height + 16.0 * num);
		}
	}

	public double DialogueWidth
	{
		get
		{
			if (moment == null || string.IsNullOrEmpty(moment.Title))
			{
				return 0.0;
			}
			double num = Math.Max(0.9, Math.Min(1.2, NotchConfig.Instance.SizeScale));
			double num2 = (moment.Suggestion ? (BubbleText(moment.PrimaryLabel, 11.0 * num, PixelsPerDip).Width + BubbleText(moment.SecondaryLabel, 11.0 * num, PixelsPerDip).Width + 18.0) : 11.0);
			CapsuleWindow.PetEventLook(ActivityType.CompanionMessage, moment.Id, moment.Pose == "plug", out var glyph, out var _, out var _);
			if (glyph != null)
			{
				num2 += 30.0 * num;
			}
			return Math.Min(560.0 * num, Math.Ceiling((BubbleText(moment.Title, 15.0 * num, PixelsPerDip).Width + num2 + 38.0) / 0.8285714285714285));
		}
	}

	public Rect PrimaryButtonBounds
	{
		get
		{
			EnsureBubbleLayout();
			return primaryBounds;
		}
	}

	public Rect SecondaryButtonBounds
	{
		get
		{
			EnsureBubbleLayout();
			return secondaryBounds;
		}
	}

	public string ActivityName
	{
		get
		{
			if (activity != null)
			{
				return activity.Name;
			}
			return null;
		}
	}

	public PetSegment Segment => segment;

	public System.Windows.Shapes.Path NotchSurface { get; set; }

	public Geometry StageShape { get; set; }

	public double NotchOpacity
	{
		get
		{
			return notchOpacity;
		}
		set
		{
			if (!(Math.Abs(notchOpacity - value) < 0.0005))
			{
				notchOpacity = value;
				InvalidateVisual();
			}
		}
	}

	public string SceneName
	{
		get
		{
			if (scene != null)
			{
				return scene.Name;
			}
			return "idle";
		}
	}

	public double RobotX => x;

	public bool HasExited => departed;

	public long ClickCount { get; private set; }

	public long SceneFrames { get; private set; }

	public double SceneClockMilliseconds { get; private set; }

	public static long CachedSceneFrames { get; private set; }

	public static long DynamicRasterFrames { get; private set; }

	private bool LifeLoop
	{
		get
		{
			if (!baking && scene == null && !departed)
			{
				if (!L.Animating)
				{
					return smoothing;
				}
				return true;
			}
			return false;
		}
	}

	protected override bool Loop
	{
		get
		{
			if (scene == null && reaction == null)
			{
				return LifeLoop;
			}
			return true;
		}
	}

	protected override Point CursorAnchor => new Point(x * base.ActualWidth / 280.0, base.ActualHeight * 0.3);

	private double Home
	{
		get
		{
			if (activity == null)
			{
				if (incoming == null)
				{
					return 140.0;
				}
				return WorkX(incoming);
			}
			return WorkX(activity);
		}
	}

	public long LifeFrames { get; private set; }

	public long BodyBuilds { get; private set; }

	private bool SceneMoves
	{
		get
		{
			if (scene != null)
			{
				if (!(scene.Name == "dance"))
				{
					return Math.Abs(scene.At("x", elapsed + 0.05, x) - scene.At("x", elapsed, x)) > 0.02;
				}
				return true;
			}
			return false;
		}
	}

	protected override bool UsesCompositionClock => SceneMoves;

	protected override double MotionSpeed
	{
		get
		{
			if (scene != null)
			{
				return Math.Abs(scene.At("x", elapsed + 0.05, x) - scene.At("x", elapsed, x)) / 0.05;
			}
			return 0.0;
		}
	}

	protected override double FrameInterval
	{
		get
		{
			bool limitFrameRate = PowerSaver.LimitFrameRate;
			if (scene == null && reaction == null)
			{
				if (!base.EyesAnimating && !smoothing)
				{
					return (PropsAnimating || L.ActivityMotion) ? 330 : 250;
				}
				if (!limitFrameRate)
				{
					return 33.3334;
				}
				return 50.0;
			}
			if (SceneMoves)
			{
				if (!limitFrameRate)
				{
					return 8.0;
				}
				return 33.3334;
			}
			if (scene != null && reaction == null && scene.Tracks.All((KeyValuePair<string, GardenTrack> p) => Math.Abs(p.Value.At(elapsed + 0.1) - p.Value.At(elapsed)) < 0.001))
			{
				return 100.0;
			}
			if (!limitFrameRate)
			{
				return 50.0;
			}
			return 66.6667;
		}
	}

	private bool Music
	{
		get
		{
			if (PetEngine.Instance.IsMusicPlaying)
			{
				return PetPreferences.Instance.DanceToMusic;
			}
			return false;
		}
	}

	protected override int VisualChildrenCount
	{
		get
		{
			if (!visualsAttached)
			{
				return 0;
			}
			return 4;
		}
	}

	private double PixelsPerDip
	{
		get
		{
			try
			{
				return VisualTreeHelper.GetDpi(this).PixelsPerDip;
			}
			catch
			{
				return 1.0;
			}
		}
	}

	private PetLife L => QaLife ?? PetEngine.Instance.Life;

	private PetHistory History => PetEngine.Instance.History;

	private int RocketDay
	{
		get
		{
			if (QaRocket < 0)
			{
				return History.RocketDay;
			}
			return QaRocket;
		}
	}

	private int PlantDay
	{
		get
		{
			if (QaPlant < 0)
			{
				return History.PlantDay;
			}
			return QaPlant;
		}
	}

	private int StarJar
	{
		get
		{
			if (QaStars < 0)
			{
				return History.StarJar;
			}
			return QaStars;
		}
	}

	private string FestivalNow => QaFestival ?? festival;

	private string WeatherNow => QaWeather ?? weather;

	private string OutcomeNow
	{
		get
		{
			object obj = QaOutcome;
			if (obj == null)
			{
				if (segment != null)
				{
					return segment.Outcome;
				}
				obj = null;
			}
			return (string)obj;
		}
	}

	private string DrawingNow
	{
		get
		{
			object obj = QaDrawing;
			if (obj == null)
			{
				if (segment != null)
				{
					return segment.Drawing;
				}
				obj = null;
			}
			return (string)obj;
		}
	}

	private bool PropsAnimating
	{
		get
		{
			if (baking || scene != null || departed)
			{
				return false;
			}
			PetLife l = L;
			if (l.Action == null)
			{
				return false;
			}
			if (activity == null || !l.ActivityMotion)
			{
				return Array.IndexOf(fxActions, l.Action) >= 0;
			}
			return true;
		}
	}

	private double BoxX => P((introBox && (scene == null || !scene.Tracks.ContainsKey("box.x"))) ? 150.0 : Value("box.x", 340.0));

	public event EventHandler DialogueChanged;

	private void NotifyDialogue()
	{
		this.DialogueChanged?.Invoke(this, EventArgs.Empty);
	}

	private void SeedSmoothing(double l, double r, double t, double s)
	{
		smL = l;
		smR = r;
		smT = t;
		smS = s;
		smReady = true;
		smTime = PetEngine.Instance.LifeSeconds;
		smoothing = true;
	}

	private double SmoothedPart(string part, double rest)
	{
		return part switch
		{
			"sleep" => 0.0, 
			"jump" => 0.0, 
			"sit" => smS, 
			"head" => smT, 
			"armR" => smR, 
			"armL" => smL, 
			_ => rest, 
		};
	}

	private double P(double stageX)
	{
		return stageX * kx;
	}

	internal static double WorkX(PetActivity a)
	{
		if (a == null || !workX.TryGetValue(a.Prop, out var value))
		{
			return 78.0;
		}
		return value;
	}

	private void UpdateUnits()
	{
		if (base.ActualHeight > 0.0 && base.ActualWidth > 0.0)
		{
			unit = base.ActualHeight / 40.0;
			kx = base.ActualWidth / (280.0 * unit);
		}
	}

	private MatrixTransform StageTransform()
	{
		UpdateUnits();
		if (stageTransform == null || stageTransform.Matrix.M11 != unit)
		{
			stageTransform = new MatrixTransform(unit, 0.0, 0.0, unit, 0.0, 0.0);
			stageTransform.Freeze();
		}
		return stageTransform;
	}

	protected override void OnPointer(Point screen, Point centre)
	{
		PetLife life = PetEngine.Instance.Life;
		bool near = life.Near;
		bool onRobot = life.OnRobot;
		life.Cursor(screen, centre, IsRobotHit(PointFromScreen(screen)), PetEngine.Instance.LifeSeconds);
		if (life.Near || near || life.OnRobot != onRobot)
		{
			StartClock();
		}
	}

	protected override void KeepClockSteady()
	{
		if (visualsAttached)
		{
			eyeVisual.Opacity = ((eyeVisual.Opacity == 1.0) ? 0.9999 : 1.0);
		}
	}

	private void ChargeScene()
	{
		if (!(sceneStarted < 0.0))
		{
			nextSceneAt = Math.Max(nextSceneAt, budgetClock.Elapsed.TotalSeconds + 8.0 * Math.Max(0.0, budgetClock.Elapsed.TotalSeconds - sceneStarted));
			sceneStarted = -1.0;
		}
	}

	public GardenView()
		: this(baking: false)
	{
	}

	private GardenView(bool baking)
		: base("idle", 40.0, vectorOnly: true)
	{
		this.baking = baking;
		RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.LowQuality);
		base.Width = 280.0;
		base.IsHitTestVisible = true;
		base.Cursor = Cursors.Hand;
		SizeChangedEventHandler value = delegate
		{
			InvalidateStageClip();
		};
		base.SizeChanged += value;
		director.Interval = TimeSpan.FromSeconds(1.0);
		director.Tick += delegate
		{
			try
			{
				Direct();
			}
			catch (Exception ex)
			{
				TransactionLogger.Warn("Pet director recovered: " + ex);
				OnPetFailure(ex);
			}
		};
		base.Loaded += delegate
		{
			attached = true;
			SyncStage();
		};
		base.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			if (IsInteractive(e.GetPosition(this)))
			{
				e.Handled = true;
			}
		};
		base.MouseMove += delegate(object s, MouseEventArgs e)
		{
			ObserveStagePointer(e.GetPosition(this));
		};
		base.AllowDrop = true;
		base.DragOver += delegate(object s, DragEventArgs e)
		{
			e.Effects = (e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None);
			e.Handled = true;
		};
		base.Drop += delegate(object s, DragEventArgs e)
		{
			if (e.Data.GetData(DataFormats.FileDrop) is string[] array)
			{
				FileShelfService.Instance.AddFiles(array);
				PetEngine.Instance.Enqueue(new PetMoment
				{
					Id = "shelf",
					Pose = "box",
					Title = "Saved to Shelf"
				});
			}
			e.Handled = true;
		};
		AutomationProperties.SetName(this, "Ayn Pet Garden stage");
	}

	public bool HandleStageClick(Point point)
	{
		EnsureBubbleLayout();
		if (HasActionPrompt)
		{
			if (PromptButtonsReady && (primaryBounds.Contains(point) || secondaryBounds.Contains(point)))
			{
				bool flag = primaryBounds.Contains(point);
				answered = true;
				PetMoment petMoment = moment;
				base.ExpressionOverride = PetExpression.Happy;
				reaction = new GardenScene(flag ? "happy" : "hug", 1.6).Track("armL", 0.0, -15.0, 0.4, -80.0, 1.0, -80.0, 1.6, -15.0).Track("armR", 0.0, 15.0, 0.4, 80.0, 1.0, 80.0, 1.6, 15.0);
				reactionElapsed = 0.0;
				elapsed = scene.Duration - 1.75;
				PrepareFlipbook();
				StartClock();
				ClickCount++;
				Log("answer-reaction " + (flag ? "happy" : "hug"));
				PetEngine.Instance.Answer(petMoment, flag);
			}
			return true;
		}
		if (!IsInteractive(point))
		{
			return false;
		}
		ClickCount++;
		Log("click");
		if (base.Available)
		{
			PetEngine instance = PetEngine.Instance;
			instance.Life.Click(instance.LifeSeconds);
			if (scene != null)
			{
				React();
			}
			StartClock();
		}
		return true;
	}

	private bool IsInteractive(Point point)
	{
		UpdateUnits();
		double num = point.X / unit;
		double num2 = point.Y / unit;
		EnsureBubbleLayout();
		if (!(Math.Abs(num - P(x)) <= 18.0) || !(num2 >= 4.0) || !(num2 <= 40.0))
		{
			if (moment != null && moment.Suggestion && Math.Abs(Value("bubble.x", -220.0) - 177.0) < 2.0)
			{
				if (!primaryBounds.Contains(point))
				{
					return secondaryBounds.Contains(point);
				}
				return true;
			}
			return false;
		}
		return true;
	}

	private static Brush Paint(string color)
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
		solidColorBrush.Freeze();
		return solidColorBrush;
	}

	protected override void OnPetDetached()
	{
		attached = false;
		director.Stop();
		lastTick = 0.0;
		StopFlipbook();
	}

	protected override void OnPetFailure(Exception error)
	{
		scene = null;
		reaction = null;
		moment = null;
		bubbleText = null;
		held.Clear();
		StopFlipbook();
		onStage = true;
		departed = false;
		x = (previousX = Home);
		NotifyDialogue();
		InvalidateVisual();
		Action action = afterExit;
		afterExit = null;
		if (action != null)
		{
			base.Dispatcher.BeginInvoke(action);
		}
	}

	protected override void OnPetSync()
	{
		SyncStage();
	}

	protected override void OnCursorWave()
	{
		React();
	}

	private void SyncStage()
	{
		if (director == null || !attached)
		{
			return;
		}
		if (!base.Available)
		{
			PetEngine.Instance.Life.Step(PetEngine.Instance.LifeSeconds, allowed: false, PetEngine.Instance.Clock().Hour);
			director.Stop();
			lastTick = 0.0;
			StopFlipbook();
			if (afterExit != null)
			{
				bool flag = moment != null;
				if (scene != null)
				{
					Log("end " + scene.Name + " policy-suspended");
				}
				scene = null;
				reaction = null;
				moment = null;
				bubbleText = null;
				held.Clear();
				x = (previousX = -65.0);
				departed = true;
				if (flag)
				{
					NotifyDialogue();
				}
				Action method = afterExit;
				afterExit = null;
				base.Dispatcher.BeginInvoke(method);
			}
			else if (PetEngine.Instance.Quiet)
			{
				bool flag2 = moment != null;
				DropActivity();
				if (scene != null)
				{
					Log("end " + scene.Name + " policy-suspended");
				}
				scene = null;
				reaction = null;
				moment = null;
				bubbleText = null;
				held.Clear();
				headphones = (pillow = false);
				onStage = true;
				departed = false;
				x = (previousX = Home);
				if (flag2)
				{
					NotifyDialogue();
				}
			}
			return;
		}
		if (!director.IsEnabled)
		{
			director.Start();
		}
		if (activityLevel != PetPreferences.Instance.ActivityLevel)
		{
			activityLevel = PetPreferences.Instance.ActivityLevel;
			SetNextStory();
		}
		if (!onStage && !departed)
		{
			onStage = true;
			if (PetEngine.Instance.FirstMeetPending)
			{
				x = (previousX = 340.0);
				Play(GardenScenes.FirstMeet());
			}
			else
			{
				Play(new GardenScene("enter", 2.0).Track("x", 0.0, -32.0, 2.0, Home));
			}
			SetNextStory();
		}
		else if (scene != null)
		{
			StartClock();
		}
		if (wake != PetEngine.Instance.WakeSequence)
		{
			wake = PetEngine.Instance.WakeSequence;
			nextSceneAt = Math.Min(nextSceneAt, budgetClock.Elapsed.TotalSeconds);
			if (pillow || (scene != null && scene.Name == "pillow"))
			{
				pillow = true;
				RemoveAccessory("pillow");
			}
		}
	}

	private void SetNextStory()
	{
		string text = PetPreferences.Instance.ActivityLevel;
		double num = ((text == "Calm") ? 45 : ((text == "Playful") ? 15 : 20));
		double num2 = ((text == "Calm") ? 90 : ((text == "Playful") ? 35 : 60));
		nextStory = DateTime.UtcNow.AddSeconds(num + random.NextDouble() * (num2 - num));
	}

	private void Direct()
	{
		if (!base.Available)
		{
			SyncStage();
			return;
		}
		Account();
		if (departed || afterExit != null || scene != null)
		{
			return;
		}
		if (moment != null)
		{
			moment = null;
			bubbleText = (primaryText = (secondaryText = null));
			base.ExpressionOverride = null;
			NotifyDialogue();
		}
		PetLife life = PetEngine.Instance.Life;
		life.Step(PetEngine.Instance.LifeSeconds, allowed: true, PetEngine.Instance.Clock().Hour);
		if (life.Animating)
		{
			StartClock();
		}
		PetEngine instance = PetEngine.Instance;
		if (life.Presence == PetPresence.Sleeping)
		{
			return;
		}
		bool flag = life.Calm(instance.LifeSeconds);
		bool flag2 = !flag && budgetClock.Elapsed.TotalSeconds >= nextSceneAt;
		if (flag2 && DialogueSurfaceReady)
		{
			if (!instance.InQuietHours)
			{
				instance.Ambient();
			}
			PetMoment petMoment = instance.TakeMoment();
			if (petMoment != null)
			{
				Present(petMoment);
				return;
			}
		}
		if (instance.InQuietHours)
		{
			if (!pillow)
			{
				DropActivity();
				Play(GardenScenes.Accessory("pillow", x, bring: true));
			}
		}
		else if (headphones && !Music)
		{
			RemoveAccessory("headphones");
		}
		else if (pillow)
		{
			RemoveAccessory("pillow");
		}
		else if (instance.FirstMeetPending && !flag)
		{
			if (activity != null)
			{
				Play(GardenScenes.PutAway(activity.Prop, Anchor(activity), x));
				return;
			}
			x = (previousX = 340.0);
			Play(GardenScenes.FirstMeet());
		}
		else
		{
			DayLife(flag2);
		}
	}

	private void Account()
	{
		DateTime utcNow = DateTime.UtcNow;
		double num = ((lastAccount == DateTime.MinValue) ? 0.0 : Math.Min(2.0, (utcNow - lastAccount).TotalSeconds));
		lastAccount = utcNow;
		PetLife life = PetEngine.Instance.Life;
		string key = ((life.Presence >= PetPresence.Dozing || pillow) ? "resting" : ((scene != null) ? SceneKind(scene.Name) : ((activity == null) ? "standing" : ("activity:" + activity.Name))));
		ran.TryGetValue(key, out var value);
		ran[key] = value + num;
		if ((utcNow - summaryStart).TotalMinutes >= 60.0)
		{
			Log("hour summary " + DirectorSummary(reset: true));
		}
	}

	private static string SceneKind(string name)
	{
		if (name.StartsWith("bring-", StringComparison.Ordinal) || name.StartsWith("swap-", StringComparison.Ordinal) || name.StartsWith("away-", StringComparison.Ordinal) || name.StartsWith("return-", StringComparison.Ordinal) || name == "enter" || name == "exit")
		{
			return "transition";
		}
		if (name.StartsWith("moment-", StringComparison.Ordinal))
		{
			return "message";
		}
		switch (name)
		{
		case "hello":
		case "wave":
		case "dance":
			return "reaction";
		default:
			return "scene:" + name;
		}
	}

	internal string DirectorSummary(bool reset)
	{
		double num = ran.Values.Sum();
		double value = 0.0;
		double value2 = 0.0;
		ran.TryGetValue("resting", out value);
		ran.TryGetValue("standing", out value2);
		double num2 = Math.Max(1E-09, num - value);
		IEnumerable<string> values = from p in ran
			orderby p.Value descending
			select p.Key + "=" + Math.Round(p.Value) + "s";
		string result = "awake=" + Math.Round(num2) + "s standing=" + Math.Round(value2) + "s (" + (100.0 * value2 / num2).ToString("F1", CultureInfo.InvariantCulture) + "%) " + string.Join(" ", values);
		if (reset)
		{
			ran.Clear();
			summaryStart = DateTime.UtcNow;
		}
		return result;
	}

	private void LogActivityChange()
	{
		string text = ((activity == null) ? null : activity.Name);
		if (!(text == loggedActivity))
		{
			if (loggedActivity != null)
			{
				Log("activity end " + loggedActivity + " " + Math.Round((DateTime.UtcNow - activitySince).TotalSeconds) + "s");
			}
			if (text != null)
			{
				Log("activity start " + text);
			}
			loggedActivity = text;
			activitySince = DateTime.UtcNow;
		}
	}

	private void DayLife(bool open)
	{
		PetEngine instance = PetEngine.Instance;
		double totalSeconds = budgetClock.Elapsed.TotalSeconds;
		if (instance.ReturnWave)
		{
			instance.ReturnWave = false;
			React();
			return;
		}
		if (Music != lastMusic)
		{
			lastMusic = Music;
			musicChanged = true;
			if (segment != null && segment.Kind != PetSegmentKind.Big)
			{
				instance.Day.EndActivity();
				segmentEnd = totalSeconds;
			}
		}
		if (segment == null || totalSeconds >= segmentEnd)
		{
			PetContext petContext = instance.Context();
			segment = instance.Day.Next(petContext);
			walked = false;
			segmentEnd = totalSeconds + segment.Seconds + ((segment.Kind == PetSegmentKind.Activity) ? 6.0 : 0.0);
			instance.Life.DayMood = instance.Day.Mood(petContext);
			UpdateWear(petContext);
			if (weather != petContext.Weather)
			{
				weather = petContext.Weather;
				idleBodyKey = null;
				InvalidateVisual();
			}
			Log("segment " + segment);
			if (segment.Kind == PetSegmentKind.Micro)
			{
				instance.Life.Queue(segment.Micros, instance.LifeSeconds, segment.Seconds);
				if (Music && instance.Clock().DayOfWeek == DayOfWeek.Friday)
				{
					Dance();
					return;
				}
			}
			if (segment.Kind == PetSegmentKind.Big || segment.Kind == PetSegmentKind.Story)
			{
				pendingBig = segment;
				pendingSince = totalSeconds;
				bigCommitted = false;
			}
		}
		if (pendingBig != null)
		{
			if (open)
			{
				bigCommitted = true;
			}
			if (bigCommitted)
			{
				if (activity != null && doneFor != activity.Name && (DateTime.UtcNow - activitySince).TotalSeconds >= 60.0)
				{
					doneFor = activity.Name;
					Play(GardenScenes.Done(activity.Prop, x));
					return;
				}
				if (activity != null)
				{
					Play(GardenScenes.PutAway(activity.Prop, Anchor(activity), x));
					return;
				}
				PetSegment petSegment = pendingBig;
				pendingBig = null;
				bigCommitted = false;
				segment = petSegment;
				segmentEnd = totalSeconds + petSegment.Seconds;
				walked = true;
				if (petSegment.Kind == PetSegmentKind.Story)
				{
					Play(GardenScenes.Launch(x));
					return;
				}
				bigScene = petSegment;
				Play(GardenScenes.Big(petSegment.Name, petSegment.Outcome, x));
				return;
			}
			if (totalSeconds - pendingSince >= 300.0)
			{
				Log("big dropped " + pendingBig.Name + " after " + Math.Round(totalSeconds - pendingSince) + "s");
				if (segment == pendingBig)
				{
					segmentEnd = totalSeconds;
				}
				pendingBig = null;
				return;
			}
			if (segment == pendingBig)
			{
				segmentEnd = totalSeconds;
				return;
			}
		}
		if (segment.Kind == PetSegmentKind.Walk || segment.Kind == PetSegmentKind.Micro)
		{
			if (activity != null)
			{
				Play(GardenScenes.PutAway(activity.Prop, Anchor(activity), x));
			}
			else if (segment.Kind == PetSegmentKind.Walk && !walked)
			{
				walked = true;
				Play(GardenScenes.Walk(x, WalkTarget()));
			}
			return;
		}
		PetActivity petActivity = Wanted();
		if (petActivity != null && petActivity != activity)
		{
			if (activity != null && doneFor != activity.Name && !musicChanged && (DateTime.UtcNow - activitySince).TotalSeconds >= 60.0)
			{
				doneFor = activity.Name;
				Play(GardenScenes.Done(activity.Prop, x));
			}
			else
			{
				musicChanged = false;
				incoming = petActivity;
				Play((activity == null) ? GardenScenes.Bring(petActivity.Prop, Anchor(petActivity), x, WorkX(petActivity)) : GardenScenes.Swap(activity.Prop, Anchor(activity), petActivity.Prop, Anchor(petActivity), x, WorkX(petActivity)));
			}
		}
	}

	private double WalkTarget()
	{
		double num = x;
		for (int i = 0; i < 8; i++)
		{
			if (!(Math.Abs(num - x) < 45.0))
			{
				break;
			}
			num = 30.0 + random.NextDouble() * 220.0;
		}
		return num;
	}

	private PetActivity Wanted()
	{
		if (segment == null)
		{
			return null;
		}
		if (segment.Kind == PetSegmentKind.Big)
		{
			if (!aftermaths.TryGetValue(segment.Aftermath, out var value))
			{
				string aftermath = segment.Aftermath;
				double right = aftermath switch
				{
					"bat" => 60, 
					"hat" => 80, 
					"yoyo" => 85, 
					"wand" => 90, 
					"umbrella" => 100, 
					"kite" => 120, 
					_ => 15, 
				};
				value = new PetActivity("after-" + aftermath, aftermath, (aftermath == "guitar") ? 1 : 0, -15.0, right, 0.0);
				aftermaths[aftermath] = value;
			}
			return value;
		}
		return PetContent.Activity(segment.Activity ?? PetEngine.Instance.History.LastActivity);
	}

	private static double Anchor(PetActivity a)
	{
		return 184.0;
	}

	private void ApplyBase()
	{
		PetLife life = PetEngine.Instance.Life;
		if (!QaMode)
		{
			LogActivityChange();
		}
		RefreshWear(wearHat, wearNeck);
		if (activity == null)
		{
			life.ClearBase();
		}
		else
		{
			life.SetBase(activity.Name, activity.Sit, activity.Left, activity.Right, activity.Tilt);
		}
	}

	private void DropActivity()
	{
		activity = (incoming = null);
		pendingBig = null;
		bigCommitted = false;
		held.Remove("act.x");
		held.Remove("next.x");
		PetEngine.Instance.Life.ClearBase();
		if (!QaMode)
		{
			LogActivityChange();
		}
	}

	private void UpdateWear(PetContext c)
	{
		festival = PetCalendar.Festival(c.Now, c.Birthday);
		string hat = ((festival == "christmas") ? "santa" : ((festival == "birthday" || festival == "newyear") ? "party" : null));
		string neck = ((c.Weather == "cold") ? "scarf" : null);
		RefreshWear(hat, neck);
	}

	private void RefreshWear(string hat, string neck)
	{
		wearHat = hat;
		wearNeck = neck;
		bool flag = headphones || (activity != null && activity.Prop == "headphones") || (incoming != null && incoming.Prop == "headphones" && scene != null && scene.Name.StartsWith("bring-", StringComparison.Ordinal) && elapsed > 2.6);
		string festivalNow = FestivalNow;
		string text = hat + "/" + neck + "/" + (festivalNow == "holi") + "/" + flag;
		if (!(text == wearKey) && rig != null)
		{
			wearKey = text;
			rig.SetWear(hat, neck, festivalNow == "holi", flag);
			idleBodyKey = null;
			InvalidateVisual();
		}
	}

	public static double HoldSeconds(string text)
	{
		return Math.Min(6.0, 2.5 + 0.06 * (double)(text ?? "").Length);
	}

	private static FormattedText Text(string text, double size, Brush brush)
	{
		return new FormattedText(text ?? "", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, brush);
	}

	private void Present(PetMoment next)
	{
		moment = next;
		answered = false;
		base.ExpressionOverride = next.Expression;
		NotifyDialogue();
		bubbleLayoutKey = null;
		EnsureBubbleLayout();
		double num = (next.Suggestion ? 13.0 : HoldSeconds(next.Title));
		double num2 = 2.5 + num + 1.5;
		GardenScene gardenScene = new GardenScene("moment-" + next.Id, num2).Track("x", 0.0, x, 0.4, x, 2.0, 30.0, num2 - 1.4, 30.0, num2, Home).Track("head", 0.0, 0.0, 0.4, 0.0, 1.2, -6.0, 2.0, 5.0, num2 - 1.4, 5.0, num2 - 0.8, 0.0, num2, 0.0).Track("armR", 0.0, 15.0, 0.4, 15.0, 1.2, 75.0, 2.0, 75.0, num2 - 1.0, 75.0, num2, 15.0);
		if (activity != null)
		{
			double num3 = Anchor(activity);
			gardenScene.Track("act.x", 0.0, num3, 0.3, num3, 1.6, 340.0, num2 - 1.6, 340.0, num2 - 0.2, num3, num2, num3);
		}
		if (!string.IsNullOrEmpty(next.Title))
		{
			gardenScene.Track("bubble.x", 0.0, -98.0, 2.0, -98.0, 2.25, 177.0, 2.25 + num, 177.0, 2.5 + num, -98.0, num2, -98.0);
		}
		if (next.Id == "greeting")
		{
			gardenScene.Track("armL", 0.0, -15.0, 2.0, -15.0, 2.3, -130.0, 2.7, -80.0, 3.0, -130.0, 3.4, -15.0);
		}
		if (next.Expression == PetExpression.Happy)
		{
			gardenScene.Track("jump", 0.0, 0.0, 2.0, 0.0, 2.15, 1.0, 2.4, -4.0, 2.7, 0.0, 2.9, 1.0, 3.1, 0.0);
		}
		if (next.Pose == "paper")
		{
			gardenScene.Track("paper.x", 0.0, -40.0, 1.0, -40.0, 2.0, 42.0, 3.0, 42.0, 4.0, -40.0, 6.0, -40.0).Track("paper.y", 0.0, 7.0, 2.0, 7.0, 2.5, 24.0, 4.0, 24.0, 6.0, 24.0);
		}
		if (next.Pose == "ears")
		{
			gardenScene.Track("armL", 0.0, -15.0, 2.0, -15.0, 2.4, -145.0, 3.5, -145.0, 4.0, -15.0);
		}
		if (next.Pose != "paper" && next.Pose != "wave" && next.Pose != "hug")
		{
			double num4 = ((next.Pose == "plug") ? 35 : 42);
			gardenScene.Track("reactionProp.x", 0.0, -206.0, 1.0, -206.0, 2.0, 42.0, 2.8, num4, num2 - 2.0, 42.0, num2 - 1.0, -206.0, num2, -206.0).Track("reactionProp.y", 0.0, 23.0, 2.0, 23.0, 2.8, (next.Pose == "camera") ? 17 : 23, num2 - 2.0, 23.0);
			if (next.Pose == "camera")
			{
				gardenScene.Track("flash", 0.0, 0.0, 2.7, 0.0, 2.76, 1.0, 2.95, 0.0);
			}
		}
		if (next.Pose == "device")
		{
			gardenScene.Track("armL", 0.0, -15.0, 2.0, -15.0, 2.3, -130.0, 2.7, -80.0, 3.0, -130.0, 3.4, -15.0);
		}
		Play(gardenScene);
	}

	public bool IsRobotHit(Point point)
	{
		UpdateUnits();
		double num = point.X / unit;
		double num2 = point.Y / unit;
		if (!departed && Math.Abs(num - P(x)) <= 16.0 && num2 >= 7.5)
		{
			return num2 <= 38.5;
		}
		return false;
	}

	public bool BlocksHover(Point point)
	{
		if (base.Available)
		{
			if (!HasActionPrompt)
			{
				return IsRobotHit(point);
			}
			return true;
		}
		return false;
	}

	public void ObserveStagePointer(Point point)
	{
		if (base.Available)
		{
			PetEngine instance = PetEngine.Instance;
			if (instance.Life.Rub(point.X, IsRobotHit(point), instance.LifeSeconds))
			{
				StartClock();
			}
		}
	}

	public bool TryAcceptFiles(Point point, string[] files)
	{
		if (!base.Available || !IsInteractive(point) || files == null)
		{
			return false;
		}
		FileShelfService.Instance.AddFiles(files);
		PetEngine.Instance.Enqueue(new PetMoment
		{
			Id = "shelf",
			Pose = "box",
			Title = "Saved to Shelf"
		});
		return true;
	}

	public void Hover()
	{
		LookUp();
		React();
	}

	public void React()
	{
		if (base.Available && !departed && afterExit == null && reaction == null)
		{
			if (scene != null)
			{
				reaction = new GardenScene("wave", 1.2).Track("armL", 0.0, 0.0, 0.2, -110.0, 0.4, -80.0, 0.6, -130.0, 0.8, -85.0, 1.2, 0.0).Track("head", 0.0, 0.0, 0.2, -4.0, 0.4, 3.0, 0.7, -2.0, 1.2, 0.0);
				reactionElapsed = 0.0;
				PrepareFlipbook();
				Log("reaction start wave");
				StartClock();
			}
			else
			{
				Play(new GardenScene("hello", 1.6).Track("x", 0.0, x, 1.6, x).Track("armL", 0.0, -15.0, 0.3, -140.0, 0.6, -110.0, 0.9, -150.0, 1.2, -110.0, 1.6, -15.0).Track("jump", 0.0, 0.0, 0.2, 2.0, 0.5, -5.0, 0.9, 0.0, 1.1, 1.0, 1.6, 0.0));
			}
		}
	}

	private void Dance()
	{
		GardenScene gardenScene = new GardenScene("dance", 2.4).Track("x", 0.0, x, 0.6, x - 8.0, 1.2, x, 1.8, x + 8.0, 2.4, x);
		gardenScene.Track("armL", 0.0, -15.0, 0.6, -100.0, 1.2, -15.0, 1.8, -100.0, 2.4, -15.0).Track("armR", 0.0, 15.0, 0.6, 100.0, 1.2, 15.0, 1.8, 100.0, 2.4, 15.0);
		Play(gardenScene);
	}

	private void RemoveAccessory(string name)
	{
		GardenScene gardenScene = new GardenScene("return-" + name, 6.0).Track("x", 0.0, x, 3.0, -40.0, 4.0, -40.0, 6.0, Home);
		gardenScene.Track(name + ".x", 0.0, (name == "headphones") ? x : Value(name + ".x", x), 3.0, -40.0, 6.0, -40.0).Track(name + ".y", 0.0, Value(name + ".y", 12.0), 1.0, 23.0, 6.0, 23.0);
		Play(gardenScene);
	}

	public void ReturnToStage()
	{
		if (departed)
		{
			departed = false;
			onStage = false;
			held.Clear();
			reaction = null;
			headphones = (pillow = false);
			x = (previousX = -32.0);
			DropActivity();
			SyncStage();
		}
	}

	public bool Exit(Action done)
	{
		return ExitTimed(done, 1.4);
	}

	internal bool ExitForHover(Action done)
	{
		return ExitTimed(done, 0.12);
	}

	private bool ExitTimed(Action done, double seconds)
	{
		if (departed)
		{
			return false;
		}
		if (!base.Available)
		{
			if (scene != null)
			{
				Log("end " + scene.Name + " policy-suspended");
			}
			departed = true;
			onStage = false;
			scene = null;
			reaction = null;
			held.Clear();
			x = (previousX = -65.0);
			DropActivity();
			director.Stop();
			return false;
		}
		if (afterExit != null)
		{
			afterExit = (Action)Delegate.Combine(afterExit, done);
			return true;
		}
		if (activity != null && scene == null)
		{
			held["act.x"] = Anchor(activity);
		}
		GardenScene gardenScene = new GardenScene("exit", seconds).Track("x", 0.0, x, seconds, -220.0);
		string[] array = (from k in LiveKeys()
			where k.Contains(".")
			select k).ToArray();
		foreach (string text in array)
		{
			double num = ((text == "headphones.x" && headphones) ? x : Value(text, 0.0));
			double num2 = num;
			if (text.EndsWith(".x", StringComparison.Ordinal) && num >= -24.0 && num <= 304.0)
			{
				num2 = ((text != "bubble.x" && num > 140.0) ? 340.0 : (-220.0));
			}
			gardenScene.Track(text, 0.0, num, seconds, num2);
		}
		afterExit = done;
		Play(gardenScene);
		return true;
	}

	private IEnumerable<string> LiveKeys()
	{
		return held.Keys.Concat((scene == null) ? Enumerable.Empty<string>() : scene.Tracks.Keys).Distinct();
	}

	private double Value(string name, double fallback)
	{
		double value;
		if (scene == null)
		{
			if (!held.TryGetValue(name, out value))
			{
				return fallback;
			}
			return value;
		}
		return scene.At(name, elapsed, held.TryGetValue(name, out value) ? value : fallback);
	}

	private void Play(GardenScene next)
	{
		string[] array = new string[6] { "armL", "armR", "head", "jump", "sit", "sleep" };
		foreach (string text in array)
		{
			double num = ((text == "armL") ? (-15) : ((text == "armR") ? 15 : 0));
			double num2 = ((scene == null && !baking && smReady) ? SmoothedPart(text, num) : Value(text, num));
			if (next.Tracks.TryGetValue(text, out var value))
			{
				GardenKey value2 = value.Keys[0];
				double value3 = value2.Value;
				value2.Value = num2;
				value.Keys[0] = value2;
				double num3 = ((value.Keys.Count > 1) ? Math.Min(0.4, Math.Max(0.25, value.Keys[1].Time * 0.8)) : 0.4);
				if (Math.Abs(value3 - num2) > 0.5 && (value.Keys.Count < 2 || value.Keys[1].Time > num3 + 0.05))
				{
					value.Keys.Insert(1, new GardenKey(num3, value3, GardenEase.Smooth));
				}
			}
			else
			{
				next.Track(text, 0.0, num2, 0.4, num);
			}
		}
		if (scene != null)
		{
			Capture();
			Log("end " + scene.Name + " interrupted");
		}
		if (scene == null)
		{
			sceneStarted = budgetClock.Elapsed.TotalSeconds;
		}
		scene = next;
		idleBodyKey = null;
		elapsed = 0.0;
		step = 0.0;
		lastTick = 0.0;
		eventIndex = 0;
		PrepareFlipbook();
		Log("start " + next.Name);
		StartClock();
		InvalidateVisual();
	}

	private void Capture()
	{
		if (scene == null)
		{
			return;
		}
		foreach (string key in scene.Tracks.Keys)
		{
			held[key] = scene.At(key, elapsed, 0.0);
		}
	}

	private void SceneEnded(string ended)
	{
		PetEngine instance = PetEngine.Instance;
		string[] array = new string[18]
		{
			"act.x", "next.x", "board.x", "rod.x", "kite.x", "bat.x", "stumps.x", "guitar.x", "wand.x", "hat.x",
			"bin.x", "dominoes.x", "box.x", "rocket.x", "wad.x", "wad.y", "ball.x", "ball.y"
		};
		foreach (string key in array)
		{
			held.Remove(key);
		}
		if (ended.StartsWith("bring-", StringComparison.Ordinal) || ended.StartsWith("swap-", StringComparison.Ordinal))
		{
			activity = incoming;
			incoming = null;
			ApplyBase();
		}
		else if (ended.StartsWith("away-", StringComparison.Ordinal))
		{
			activity = null;
			ApplyBase();
		}
		else if (ended == "moment-first-hello" && introBox)
		{
			Play(GardenScenes.IntroKick(Home));
			introBox = false;
		}
		else if (ended == "first-meet")
		{
			introBox = true;
			instance.CompleteFirstMeet();
			instance.Enqueue(new PetMoment
			{
				Id = "first-hello",
				Pose = "wave",
				Title = (instance.LineFrom("first") ?? "Hi, main Ayn!"),
				Expression = PetExpression.Happy,
				Priority = 2
			});
			string text = instance.LineFrom("greet/" + PetStrings.Part(instance.Clock()));
			if (text != null)
			{
				instance.Enqueue(new PetMoment
				{
					Id = "first-greeting",
					Pose = "wave",
					Title = text,
					Expression = PetExpression.Happy,
					Priority = 3
				});
			}
			nextSceneAt = 0.0;
		}
		else if (ended == "rocket-launch")
		{
			Say("story/launch");
		}
		else if (bigScene != null && ended == bigScene.Name)
		{
			bool flag = bigScene.Outcome == PetContent.Big(bigScene.Name).Outcomes[0] || bigScene.Outcome == "six" || bigScene.Outcome == "soar";
			Say(flag ? "outcome/good" : "outcome/oops");
			bigScene = null;
		}
	}

	private void Say(string group)
	{
		PetEngine instance = PetEngine.Instance;
		DateTime dateTime = instance.Clock();
		if (PetLines.CanSpeak(instance.History, dateTime, instance.Quiet, instance.Life.Calm(instance.LifeSeconds)))
		{
			string text = instance.LineFrom(group, 4);
			if (text != null)
			{
				instance.History.LastSpontaneous = dateTime;
				instance.Enqueue(new PetMoment
				{
					Id = "say-" + group,
					Pose = "wave",
					Title = text,
					Expression = PetExpression.Happy,
					Priority = 6
				});
			}
		}
	}

	private static void Log(string line)
	{
		TransactionLogger.Info("Pet Garden: " + line);
	}

	protected override void OnPetTick(double ms)
	{
		if (moment != null && moment.Priority > 3 && PetEngine.Instance.Life.Calm(PetEngine.Instance.LifeSeconds))
		{
			moment = null;
			bubbleText = null;
			base.ExpressionOverride = null;
			NotifyDialogue();
			if (scene != null)
			{
				scene.Duration = elapsed;
			}
		}
		if (scene == null && reaction == null)
		{
			PetEngine instance = PetEngine.Instance;
			instance.Life.Step(instance.LifeSeconds, base.Available, instance.Clock().Hour);
			DrawActor(null, body: true, liveEyes: false);
			LifeFrames++;
			return;
		}
		if (lastTick != 0.0)
		{
			SceneFrames++;
			SceneClockMilliseconds += Math.Max(0.0, ms - lastTick);
		}
		double num = ((lastTick == 0.0) ? 0.0 : Math.Max(0.0, Math.Min(0.25, (ms - lastTick) / 1000.0)));
		lastTick = ms;
		if (reaction != null)
		{
			reactionElapsed += num;
			if (reactionElapsed >= reaction.Duration)
			{
				string name = reaction.Name;
				reaction = null;
				if (moment == null)
				{
					base.ExpressionOverride = null;
				}
				Log("reaction end " + name);
			}
		}
		if (scene == null)
		{
			return;
		}
		elapsed += num;
		previousX = x;
		x = Value("x", x);
		if (Math.Abs(x - previousX) > 0.015)
		{
			step += num * Math.PI * 2.0;
		}
		while (eventIndex < scene.Events.Count && elapsed >= scene.Events[eventIndex].Time)
		{
			Log("event " + scene.Name + " " + scene.Events[eventIndex++].Name);
		}
		if (scene.Name == "dance" && scene.Duration < 3.0 && !Music)
		{
			Capture();
			scene.Duration = elapsed;
		}
		if (!(elapsed >= scene.Duration))
		{
			return;
		}
		string name2 = scene.Name;
		Capture();
		Log("end " + name2);
		if (name2 == "walk")
		{
			sceneStarted = -1.0;
		}
		else
		{
			ChargeScene();
		}
		SeedSmoothing(Value("armL", -15.0), Value("armR", 15.0), Value("head", 0.0), Value("sit", 0.0));
		scene = null;
		lastTick = 0.0;
		if (moment == null)
		{
			base.ExpressionOverride = null;
		}
		if (name2 == "headphones")
		{
			headphones = true;
		}
		if (name2 == "pillow")
		{
			pillow = true;
		}
		if (name2 == "return-headphones")
		{
			headphones = false;
		}
		if (name2 == "return-pillow")
		{
			pillow = false;
		}
		SceneEnded(name2);
		StopFlipbook();
		lastFrame = BakeFrame(this, pixelScale);
		idleBodyKey = RestBodyKey();
		if (name2 == "exit")
		{
			departed = true;
			director.Stop();
			Action action = afterExit;
			afterExit = null;
			if (action != null)
			{
				base.Dispatcher.BeginInvoke(action);
			}
		}
	}

	private static void Box(DrawingContext d, Brush b, double x, double y, double w, double h, double r)
	{
		d.DrawRoundedRectangle(b, null, new Rect(x, y, Math.Max(0.01, w), Math.Max(0.01, h)), r, r);
	}

	private static void Line(DrawingContext d, Brush b, double w, double x, double y, double xx, double yy)
	{
		if (!pens.TryGetValue(b, out var value))
		{
			value = new Dictionary<double, Pen>();
			pens.Add(b, value);
		}
		if (!value.TryGetValue(w, out var value2))
		{
			Pen pen = new Pen(b, w);
			pen.StartLineCap = PenLineCap.Round;
			pen.EndLineCap = PenLineCap.Round;
			value2 = pen;
			value2.Freeze();
			value.Add(w, value2);
		}
		d.DrawLine(value2, new Point(x, y), new Point(xx, yy));
	}

	private static void Path(DrawingContext d, Brush b, string path)
	{
		if (!paths.TryGetValue(path, out var value))
		{
			value = Geometry.Parse(path);
			value.Freeze();
			paths.Add(path, value);
		}
		d.DrawGeometry(b, null, value);
	}

	public void InvalidateStageClip()
	{
		eyesReady = false;
		clipSource = null;
		renderedFrame = null;
		renderedBubble = null;
		InvalidateVisual();
	}

	private void PushShape(DrawingContext d)
	{
		if (StageShape != null)
		{
			clip = StageShape;
			if (base.Clip != StageShape)
			{
				base.Clip = StageShape;
			}
			d.PushTransform(Transform.Identity);
			return;
		}
		if (NotchSurface == null || NotchSurface.Data == null)
		{
			d.PushClip(new RectangleGeometry(new Rect(0.0, 0.0, base.ActualWidth, base.ActualHeight)));
			return;
		}
		if (clipSource != NotchSurface.Data)
		{
			GeneralTransform generalTransform = NotchSurface.TransformToVisual(this);
			Point point = generalTransform.Transform(default(Point));
			Point point2 = generalTransform.Transform(new Point(1.0, 0.0));
			Point point3 = generalTransform.Transform(new Point(0.0, 1.0));
			Matrix matrix = new Matrix(point2.X - point.X, point2.Y - point.Y, point3.X - point.X, point3.Y - point.Y, point.X, point.Y);
			clipSource = NotchSurface.Data;
			clip = clipSource.CloneCurrentValue();
			clip.Transform = new MatrixTransform(matrix);
			clip.Freeze();
		}
		d.PushClip(clip);
	}

	protected override void OnRender(DrawingContext d)
	{
		PushShape(d);
		if (notchOpacity >= 0.999)
		{
			d.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, base.ActualWidth, base.ActualHeight));
		}
		d.PushTransform(StageTransform());
		if (!visualsAttached)
		{
			AddVisualChild(propVisual);
			AddVisualChild(bodyVisual);
			AddVisualChild(eyeVisual);
			AddVisualChild(bubbleVisual);
			visualsAttached = true;
		}
		string text = RestBodyKey();
		if (lastFrame == null || (!Loop && text != idleBodyKey))
		{
			lastFrame = BakeFrame(this, pixelScale);
			idleBodyKey = text;
		}
		if (lastFrame != null && lastFrame != renderedFrame)
		{
			PresentBody(lastFrame);
		}
		PresentFrame();
		d.Pop();
		d.Pop();
	}

	private string RestBodyKey()
	{
		return x + "/" + Value("sit", 0.0) + "/" + Value("sleep", 0.0) + "/" + headphones + "/" + pillow + "/" + CapsuleMotion.AnimationsEnabled + "/" + ((activity == null) ? "" : activity.Name) + "/" + DayPropsKey();
	}

	private void StopFlipbook()
	{
		SceneAtlas.Release();
	}

	private void PrepareFlipbook()
	{
		if (!baking)
		{
			PresentationSource presentationSource = PresentationSource.FromVisual(this);
			pixelScale = ((base.ActualWidth > 0.0) ? (base.ActualWidth / 280.0) : 1.0);
			if (presentationSource != null)
			{
				pixelScale *= presentationSource.CompositionTarget.TransformToDevice.M11;
			}
			pixelScale = Math.Max(1.0, pixelScale);
		}
	}

	private static CachedFrame BakeLayer(GardenView view, double scale, int layer)
	{
		DrawingGroup drawingGroup = new DrawingGroup();
		using (DrawingContext d = drawingGroup.Open())
		{
			if (layer == 0)
			{
				view.DrawActor(d, body: true, liveEyes: false);
			}
			else
			{
				view.DrawProps(d);
			}
		}
		return Raster(drawingGroup, scale, layer != 0);
	}

	private static CachedFrame BakeFrame(GardenView view, double scale)
	{
		DrawingGroup drawingGroup = view.vectorBody ?? (view.vectorBody = new DrawingGroup());
		if (view.rig != null && view.bodyRig == view.rig && !view.baking)
		{
			view.DrawActor(null, body: true, liveEyes: false);
		}
		else
		{
			view.BodyBuilds++;
			using (DrawingContext d = drawingGroup.Open())
			{
				view.DrawActor(d, body: true, liveEyes: false);
			}
			view.bodyRig = view.rig;
		}
		DrawingGroup drawingGroup2 = view.vectorProps ?? (view.vectorProps = new DrawingGroup());
		bool flag = view.StillPropScene();
		if (!flag || view.propsScene != view.scene)
		{
			using (DrawingContext d2 = drawingGroup2.Open())
			{
				view.DrawProps(d2);
			}
			view.propsScene = (flag ? view.scene : null);
		}
		return view.vectorFrame ?? (view.vectorFrame = new CachedFrame
		{
			BodyDrawing = drawingGroup,
			PropDrawing = drawingGroup2
		});
	}

	private bool StillPropScene()
	{
		if (scene != null && reaction == null && moment == null && !pillow && !introBox && !baking)
		{
			return Array.IndexOf(stillPropScenes, scene.Name) >= 0;
		}
		return false;
	}

	private static CachedFrame Raster(DrawingGroup drawing, double scale, bool clipToStage = true)
	{
		Rect rect = (clipToStage ? Rect.Intersect(new Rect(0.0, 0.0, 280.0, 40.0), drawing.Bounds) : drawing.Bounds);
		if (rect.IsEmpty)
		{
			rect = new Rect(0.0, 0.0, 1.0, 1.0);
		}
		rect = new Rect(Math.Floor(rect.Left * scale) / scale, Math.Floor(rect.Top * scale) / scale, Math.Max(1.0 / scale, Math.Ceiling(rect.Right * scale) / scale - Math.Floor(rect.Left * scale) / scale), Math.Max(1.0 / scale, Math.Ceiling(rect.Bottom * scale) / scale - Math.Floor(rect.Top * scale) / scale));
		DrawingVisual drawingVisual = new DrawingVisual();
		using (DrawingContext drawingContext = drawingVisual.RenderOpen())
		{
			drawingContext.PushTransform(new TranslateTransform(0.0 - rect.X, 0.0 - rect.Y));
			drawingContext.DrawDrawing(drawing);
			drawingContext.Pop();
		}
		RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(Math.Max(1, (int)Math.Round(rect.Width * scale)), Math.Max(1, (int)Math.Round(rect.Height * scale)), 96.0 * scale, 96.0 * scale, PixelFormats.Pbgra32);
		renderTargetBitmap.Render(drawingVisual);
		renderTargetBitmap.Freeze();
		CachedFrame cachedFrame = new CachedFrame();
		cachedFrame.Image = renderTargetBitmap;
		cachedFrame.Bounds = rect;
		return cachedFrame;
	}

	private void DrawActor(DrawingContext d, bool body, bool liveEyes)
	{
		bool flag = baking || QaMode || CapsuleMotion.AnimationsEnabled;
		if (rig == null)
		{
			rig = new PetVectorRig(shell, shade, ink)
			{
				Scale = 1.12,
				Ground = 38.5
			};
		}
		double num = P((flag || Value("inbox", 0.0) > 0.0) ? x : Home);
		double jump = (flag ? Value("jump", 0.0) : 0.0) + Value("inbox", 0.0) * 34.0;
		bool flag2 = flag && Math.Abs(x - previousX) > 0.015;
		double gait = (flag2 ? Math.Sin(step) : 0.0);
		double num2 = (flag ? Value("armL", -15.0) : (-15.0));
		double num3 = (flag ? Value("armR", 15.0) : 15.0);
		if (flag2)
		{
			num2 += Math.Sin(step - 0.5) * 24.0;
			num3 -= Math.Sin(step - 0.5) * 24.0;
		}
		double num4 = (flag ? (Value("head", 0.0) + (flag2 ? (Math.Sin(step - 0.35) * 1.5) : 0.0)) : 0.0);
		if (flag && reaction != null)
		{
			num2 += reaction.At("armL", reactionElapsed, 0.0);
			num3 += reaction.At("armR", reactionElapsed, 0.0);
			num4 += reaction.At("head", reactionElapsed, 0.0);
		}
		PetExpression mood = ((Value("sleep", 0.0) > 0.5) ? PetExpression.Closed : ((!baking) ? base.Expression : PetExpression.Normal));
		Vector look = (flag ? base.Gaze : default(Vector));
		double num5 = (flag ? Value("sit", 0.0) : 0.0);
		double breath = 0.0;
		double eyeScale = 1.0;
		double spin = 0.0;
		double headLift = 0.0;
		if (!baking && scene == null && reaction == null && flag)
		{
			PetLife l = L;
			if (l.IdleLook.HasValue)
			{
				look = l.IdleLook.Value;
			}
			headLift = l.HeadLift;
			spin = l.Spin;
			jump = l.Jump;
			gait = l.Gait;
			num2 = l.Left;
			num3 = l.Right;
			num4 = l.Tilt;
			num5 = l.Sit;
			mood = l.Mood;
			eyeScale = l.EyeScale;
			if (body)
			{
				double lifeSeconds = PetEngine.Instance.LifeSeconds;
				double num6 = ((smTime <= 0.0) ? 1.0 : Math.Max(0.0, lifeSeconds - smTime));
				smTime = lifeSeconds;
				double num7 = (QaMode ? 1.0 : (1.0 - Math.Exp((0.0 - num6) / 0.14)));
				if (!smReady)
				{
					smL = num2;
					smR = num3;
					smT = num4;
					smS = num5;
					smReady = true;
				}
				else
				{
					smL += (num2 - smL) * num7;
					smR += (num3 - smR) * num7;
					smT += (num4 - smT) * num7;
					smS += (num5 - smS) * num7;
				}
				smoothing = Math.Abs(num2 - smL) > 0.6 || Math.Abs(num3 - smR) > 0.6 || Math.Abs(num4 - smT) > 0.3 || Math.Abs(num5 - smS) > 0.02;
			}
			if (smReady)
			{
				num2 = smL;
				num3 = smR;
				num4 = smT;
				num5 = smS;
			}
			PresentationSource presentationSource = PresentationSource.FromVisual(this);
			double num8 = ((presentationSource == null || presentationSource.CompositionTarget == null) ? 1.0 : presentationSource.CompositionTarget.TransformToDevice.M22);
			breath = Math.Round(l.Breath) * 40.0 / Math.Max(1.0, base.ActualHeight * num8 * 1.12);
		}
		if (body)
		{
			handX = num;
			handRight = num3;
			handJump = jump;
		}
		rig.Draw(d, num, jump, gait, num5, flag ? Value("sleep", 0.0) : 0.0, num2, num3, num4, look, mood, QaMode ? 0.0 : base.BlinkClosure, body, liveEyes, breath, eyeScale, spin, headLift);
		if (liveEyes && d != null && !baking && scene == null)
		{
			PetLife l2 = L;
			if (l2.Action == "purr")
			{
				d.PushTransform(new TranslateTransform(num + 21.0, 9.0));
				d.PushTransform(new ScaleTransform(1.8, 1.8));
				Path(d, pink, "M 0,2 C -4,-2 -5,4 0,7 C 5,4 4,-2 0,2 Z");
				d.Pop();
				d.Pop();
			}
			if (l2.Presence == PetPresence.Dozing)
			{
				d.PushTransform(new TranslateTransform(num + 20.0, 4.0 + l2.Breath * 2.0));
				PetArt.Zee(d, 0.0, 0.0, 7.0);
				PetArt.Zee(d, 8.0, -3.0, 5.0);
				d.Pop();
			}
		}
	}

	protected override Visual GetVisualChild(int index)
	{
		return index switch
		{
			2 => eyeVisual, 
			1 => bodyVisual, 
			0 => propVisual, 
			_ => bubbleVisual, 
		};
	}

	private void PresentBody(CachedFrame frame)
	{
		bool flag = renderedFrame == null;
		if (!flag)
		{
			bool num;
			if (frame.BodyDrawing == null)
			{
				if (renderedFrame.BodyDrawing != null || frame.Image != renderedFrame.Image)
				{
					goto IL_0068;
				}
				num = frame.Bounds != renderedFrame.Bounds;
			}
			else
			{
				num = frame.BodyDrawing != renderedFrame.BodyDrawing;
			}
			if (!num)
			{
				goto IL_00c7;
			}
		}
		goto IL_0068;
		IL_011b:
		using (DrawingContext drawingContext = propVisual.RenderOpen())
		{
			PushShape(drawingContext);
			drawingContext.PushTransform(StageTransform());
			if (frame.PropDrawing != null)
			{
				drawingContext.DrawDrawing(frame.PropDrawing);
			}
			else if (frame.Props != null)
			{
				drawingContext.DrawImage(frame.Props, frame.PropBounds);
			}
			drawingContext.Pop();
			drawingContext.Pop();
		}
		renderedProps = frame.Props;
		renderedPropBounds = frame.PropBounds;
		goto IL_019a;
		IL_00c7:
		if (!flag)
		{
			bool num2;
			if (frame.PropDrawing == null)
			{
				if (renderedFrame.PropDrawing != null || frame.Props != renderedProps)
				{
					goto IL_011b;
				}
				num2 = frame.PropBounds != renderedPropBounds;
			}
			else
			{
				num2 = frame.PropDrawing != renderedFrame.PropDrawing;
			}
			if (!num2)
			{
				goto IL_019a;
			}
		}
		goto IL_011b;
		IL_0068:
		using (DrawingContext drawingContext2 = bodyVisual.RenderOpen())
		{
			PushShape(drawingContext2);
			drawingContext2.PushTransform(StageTransform());
			if (frame.BodyDrawing != null)
			{
				drawingContext2.DrawDrawing(frame.BodyDrawing);
			}
			else
			{
				drawingContext2.DrawImage(frame.Image, frame.Bounds);
			}
			drawingContext2.Pop();
			drawingContext2.Pop();
		}
		goto IL_00c7;
		IL_019a:
		lastFrame = (renderedFrame = frame);
	}

	protected override void PresentFrame()
	{
		if (!visualsAttached)
		{
			InvalidateVisual();
			return;
		}
		PetLife l = L;
		string text = ((scene != null) ? ((scene.Name == "first-meet" || scene.Name == "intro-kick" || introBox) ? "box" : "") : ((l.Action == "purr") ? "purr" : ((l.Presence == PetPresence.Dozing) ? "doze" : "")));
		if (!eyesReady || text != "" || eyeEffect != text)
		{
			using (DrawingContext drawingContext = eyeVisual.RenderOpen())
			{
				PushShape(drawingContext);
				drawingContext.PushTransform(StageTransform());
				DrawActor(drawingContext, body: false, liveEyes: true);
				if (text == "box")
				{
					DrawBoxFront(drawingContext);
				}
				drawingContext.Pop();
				drawingContext.Pop();
			}
			eyesReady = true;
			eyeEffect = text;
		}
		else
		{
			DrawActor(null, body: false, liveEyes: true);
		}
		bool propsAnimating = PropsAnimating;
		if (scene == null && reaction == null && (propsAnimating || propsWereAnimating) && lastFrame != null && lastFrame.PropDrawing != null && lastFrame.PropDrawing == vectorProps)
		{
			using DrawingContext d = vectorProps.Open();
			DrawProps(d);
		}
		CachedFrame cachedFrame = ((scene != null || reaction != null || ((propsAnimating || propsWereAnimating) && (lastFrame == null || lastFrame.PropDrawing != vectorProps))) ? BakeFrame(this, pixelScale) : null);
		propsWereAnimating = propsAnimating;
		if (cachedFrame == null)
		{
			cachedFrame = lastFrame;
		}
		if (cachedFrame != null && cachedFrame != renderedFrame)
		{
			PresentBody(cachedFrame);
		}
		string text2 = ((moment == null) ? "" : (bubbleLayoutKey + "/" + Value("bubble.x", -98.0) + "/" + PetEngine.Instance.Quiet));
		if (text2 != renderedBubble)
		{
			using (DrawingContext drawingContext2 = bubbleVisual.RenderOpen())
			{
				PushShape(drawingContext2);
				DrawBubble(drawingContext2);
				drawingContext2.Pop();
			}
			renderedBubble = text2;
		}
	}

	public static FormattedText BubbleText(string text, double size, double pixelsPerDip = 1.0)
	{
		FormattedText formattedText = new FormattedText((text ?? "").Replace('\r', ' ').Replace('\n', ' '), CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface(AynType.Family(TypeSurface.Notch, size), FontStyles.Normal, AynType.Semibold, FontStretches.Normal), size, NotchInk.Primary, null, TextFormattingMode.Display, pixelsPerDip);
		formattedText.MaxLineCount = 1;
		formattedText.Trimming = TextTrimming.CharacterEllipsis;
		return formattedText;
	}

	private void EnsureBubbleLayout()
	{
		if (moment == null || base.ActualWidth <= 0.0 || base.ActualHeight <= 0.0)
		{
			bubbleDrawing = null;
			bubbleBounds = (primaryBounds = (secondaryBounds = Rect.Empty));
			return;
		}
		string text = base.ActualWidth + "/" + base.ActualHeight + "/" + moment.Title + "/" + moment.PrimaryLabel + "/" + moment.SecondaryLabel + "/" + moment.Suggestion + "/" + NotchConfig.Instance.Style + "/" + NotchConfig.Instance.CornerRadius;
		if (text == bubbleLayoutKey && bubbleDrawing != null)
		{
			return;
		}
		bubbleLayoutKey = text;
		double num = Math.Max(8.0, 48.0 * base.ActualWidth / 280.0 + 7.0);
		double num2 = Math.Max(1.0, base.ActualWidth - num - 8.0);
		double num3 = Math.Max(0.9, Math.Min(1.2, NotchConfig.Instance.SizeScale));
		double num4 = Math.Min(30.0 * num3, base.ActualHeight - 8.0 * num3);
		double num5 = 0.0;
		CapsuleWindow.PetEventLook(ActivityType.CompanionMessage, moment.Id, moment.Pose == "plug", out var glyph, out var tint, out var _);
		bubbleIcon = ((glyph == null) ? null : ((glyph == "camera") ? CameraImage() : LiveMessageVisuals.GlyphImage(glyph, tint)));
		double num6 = Math.Round(Math.Min(22.0 * num3, num4 - 6.0));
		if (bubbleIcon != null)
		{
			num5 = num6 + 8.0 * num3;
		}
		double pixelsPerDip = PixelsPerDip;
		bubbleText = BubbleText(moment.Title, 15.0 * num3, pixelsPerDip);
		primaryText = BubbleText(moment.PrimaryLabel, 11.0 * num3, pixelsPerDip);
		secondaryText = BubbleText(moment.SecondaryLabel, 11.0 * num3, pixelsPerDip);
		double num7 = (moment.Suggestion ? (Math.Ceiling(primaryText.Width) + 4.0) : 0.0);
		double num8 = (moment.Suggestion ? (Math.Ceiling(secondaryText.Width) + 4.0) : 0.0);
		double num9 = (moment.Suggestion ? (num7 + num8 + 10.0) : 11.0);
		double num10 = Math.Max(1.0, num2 - 11.0 - num5 - num9);
		bubbleText.MaxTextWidth = num10;
		double width = Math.Min(num2, 11.0 + num5 + Math.Min(num10, bubbleText.WidthIncludingTrailingWhitespace) + num9);
		bubbleBounds = new Rect(num, (base.ActualHeight - num4) / 2.0, width, num4);
		primaryBounds = (secondaryBounds = Rect.Empty);
		LinearGradientBrush linearGradientBrush = new LinearGradientBrush(Color.FromArgb(244, 36, 36, 40), Color.FromArgb(244, 16, 16, 19), 90.0);
		linearGradientBrush.Freeze();
		Pen pen = new Pen(Paint("#30FFFFFF"), 0.7);
		pen.Freeze();
		double num11 = NotchConfig.Instance.CornerRadius;
		if (num11 < 0.0 && (NotchConfig.Instance.Style == "Minimal" || NotchConfig.Instance.Style == "SquareMinimal"))
		{
			num11 = 5.0;
		}
		if (num11 < 0.0 && NotchConfig.Instance.Style == "Line")
		{
			num11 = 9.0;
		}
		if (!(num11 < 0.0))
		{
			Math.Min(num4 / 2.0, num11);
		}
		else
		{
			_ = num4 / 2.0;
		}
		bubbleDrawing = new DrawingGroup();
		using DrawingContext drawingContext = bubbleDrawing.Open();
		if (bubbleIcon != null)
		{
			drawingContext.DrawImage(bubbleIcon, new Rect(num + 4.0, Math.Round((base.ActualHeight - num6) / 2.0), num6, num6));
		}
		if (moment.Suggestion)
		{
			double num12 = Math.Min(19.0, num4 - 4.0);
			double y = (base.ActualHeight - num12) / 2.0;
			secondaryBounds = new Rect(bubbleBounds.Right - 4.0 - num8, y, num8, num12);
			primaryBounds = new Rect(secondaryBounds.Left - 3.0 - num7, y, num7, num12);
			drawingContext.DrawRoundedRectangle(Paint("#2CFFFFFF"), null, primaryBounds, num12 / 2.0, num12 / 2.0);
			drawingContext.DrawRoundedRectangle(Paint("#14FFFFFF"), null, secondaryBounds, num12 / 2.0, num12 / 2.0);
			drawingContext.DrawText(primaryText, new Point(primaryBounds.Left + 2.0, (base.ActualHeight - primaryText.Height) / 2.0));
			drawingContext.DrawText(secondaryText, new Point(secondaryBounds.Left + 2.0, (base.ActualHeight - secondaryText.Height) / 2.0));
		}
		double num13 = num + 11.0 + num5;
		double num14 = (moment.Suggestion ? (primaryBounds.Left - 4.0) : (bubbleBounds.Right - 11.0));
		if (num14 > num13)
		{
			drawingContext.PushClip(new RectangleGeometry(new Rect(num13, bubbleBounds.Top, num14 - num13, num4)));
			drawingContext.DrawText(bubbleText, new Point(num13, Math.Round((base.ActualHeight - bubbleText.Height) / 2.0)));
			drawingContext.Pop();
		}
	}

	private static ImageSource CameraImage()
	{
		if (cameraImage != null)
		{
			return cameraImage;
		}
		Image image = (Image)CapsuleWindow.PetCameraGlyph(24.0);
		return cameraImage = image.Source;
	}

	private void DrawBubble(DrawingContext d)
	{
		if (moment == null || PetEngine.Instance.Quiet || (HasActionPrompt && !DialogueSurfaceReady))
		{
			return;
		}
		EnsureBubbleLayout();
		if (bubbleDrawing != null)
		{
			double num = Math.Max(0.0, Math.Min(1.0, (Value("bubble.x", -98.0) + 98.0) / 275.0));
			if (!(num <= 0.0))
			{
				double num2 = num * num * (3.0 - 2.0 * num);
				double num3 = 0.96 + 0.04 * num2;
				d.PushOpacity(num2);
				d.PushTransform(new TranslateTransform((1.0 - num2) * 6.0, 0.0));
				d.PushTransform(new ScaleTransform(num3, num3, bubbleBounds.Left, bubbleBounds.Top + bubbleBounds.Height / 2.0));
				d.DrawDrawing(bubbleDrawing);
				d.Pop();
				d.Pop();
				d.Pop();
			}
		}
	}

	private MatrixTransform Legacy()
	{
		MatrixTransform matrixTransform = new MatrixTransform(kx, 0.0, 0.0, 5.0 / 6.0, 0.0, 3.3333333333333335);
		matrixTransform.Freeze();
		return matrixTransform;
	}

	private string DayPropsKey()
	{
		return RocketDay + "/" + PlantDay + "/" + StarJar + "/" + FestivalNow + "/" + wearKey + "/" + WeatherNow;
	}

	private static void DrawStrokes(DrawingContext d, PetDrawing art, Rect area, double progress, Brush brush, double width)
	{
		if (art == null || progress <= 0.0)
		{
			return;
		}
		double num = 0.0;
		foreach (Point[] stroke in art.Strokes)
		{
			for (int i = 1; i < stroke.Length; i++)
			{
				num += (stroke[i] - stroke[i - 1]).Length;
			}
		}
		double num2 = num * Math.Min(1.0, progress);
		Func<Point, Point> func = (Point u) => new Point(area.X + u.X * area.Width, area.Y + u.Y * area.Height);
		foreach (Point[] stroke2 in art.Strokes)
		{
			for (int j = 1; j < stroke2.Length; j++)
			{
				if (!(num2 > 0.0))
				{
					break;
				}
				double length = (stroke2[j] - stroke2[j - 1]).Length;
				Point point = func(stroke2[j - 1]);
				Point point2 = func(stroke2[j]);
				if (length > num2)
				{
					double num3 = num2 / length;
					point2 = new Point(point.X + (point2.X - point.X) * num3, point.Y + (point2.Y - point.Y) * num3);
				}
				Line(d, brush, width, point.X, point.Y, point2.X, point2.Y);
				num2 -= length;
			}
		}
	}

	private void DrawReactionProp(DrawingContext d)
	{
		double num = Value("reactionProp.x", -206.0);
		double num2 = Value("reactionProp.y", 23.0);
		if (num < -24.0 || num > 304.0)
		{
			return;
		}
		string text = moment.Pose;
		switch (text)
		{
		case "plug":
			Line(d, shade, 1.0, num - 6.0, num2 + 2.0, x + 1.0, 27.0);
			Box(d, shade, num - 4.0, num2 - 3.0, 7.0, 7.0, 1.0);
			Line(d, shell, 1.0, num + 3.0, num2 - 2.0, num + 6.0, num2 - 2.0);
			Line(d, shell, 1.0, num + 3.0, num2 + 2.0, num + 6.0, num2 + 2.0);
			if (moment.Expression == PetExpression.Happy)
			{
				Line(d, cyan, 1.0, num + 1.0, num2 - 10.0, num - 2.0, num2 - 6.0);
				Line(d, cyan, 1.0, num - 2.0, num2 - 6.0, num + 2.0, num2 - 6.0);
				Line(d, cyan, 1.0, num + 2.0, num2 - 6.0, num, num2 - 3.0);
			}
			break;
		case "glass":
		case "cup":
			Box(d, shell, num - 4.0, num2 - 4.0, 8.0, 9.0, 2.0);
			Box(d, cyan, num - 3.0, num2, 6.0, 4.0, 1.0);
			if (text == "cup")
			{
				d.DrawEllipse(null, new Pen(shell, 1.0), new Point(num + 5.0, num2), 2.0, 2.0);
				Line(d, shade, 0.55, num, num2 - 6.0, num + Math.Sin(elapsed * 3.0), num2 - 10.0);
			}
			break;
		case "camera":
		{
			Box(d, shade, num - 7.0, num2 - 4.0, 14.0, 10.0, 2.0);
			Box(d, shell, num - 5.0, num2 - 6.0, 5.0, 3.0, 1.0);
			d.DrawEllipse(ink, null, new Point(num, num2 + 1.0), 3.0, 3.0);
			double num5 = Value("flash", 0.0);
			if (num5 > 0.0)
			{
				d.PushOpacity(num5);
				d.DrawEllipse(Brushes.White, null, new Point(num + 5.0, num2 - 4.0), 4.0, 4.0);
				d.Pop();
			}
			break;
		}
		case "box":
			Box(d, wood, num - 7.0, num2 - 2.0, 14.0, 12.0, 1.0);
			Line(d, shade, 1.0, num, num2 - 2.0, num, num2 + 10.0);
			d.DrawEllipse(ink, null, new Point(num - 5.0, num2 + 12.0), 2.0, 2.0);
			d.DrawEllipse(ink, null, new Point(num + 5.0, num2 + 12.0), 2.0, 2.0);
			break;
		case "bell":
		{
			double angle = Math.Sin(elapsed * 15.0) * 12.0;
			d.PushTransform(new RotateTransform(angle, num, num2 - 6.0));
			Line(d, wood, 2.0, num, num2 - 6.0, num, num2 - 2.0);
			d.DrawEllipse(wood, null, new Point(num, num2 + 2.0), 5.0, 5.0);
			Line(d, shell, 1.0, num - 6.0, num2 + 6.0, num + 6.0, num2 + 6.0);
			d.Pop();
			break;
		}
		case "antenna":
		{
			Box(d, shade, num - 5.0, num2 + 5.0, 10.0, 4.0, 2.0);
			Line(d, shade, 1.0, num, num2 + 5.0, num, num2 - 5.0);
			double num4 = ((moment.Expression == PetExpression.Worried) ? 5 : 0);
			Line(d, shell, 1.0, num, num2 - 5.0, num + num4, num2 - 9.0);
			d.DrawEllipse(cyan, null, new Point(num + num4, num2 - 9.0), 1.5, 1.5);
			break;
		}
		case "device":
			Box(d, shade, num - 4.0, num2 - 7.0, 8.0, 13.0, 2.0);
			Box(d, ink, num - 3.0, num2 - 5.0, 6.0, 8.0, 1.0);
			d.DrawEllipse(cyan, null, new Point(num, num2 + 4.0), 1.0, 1.0);
			break;
		case "clock":
			d.DrawEllipse(shell, null, new Point(num, num2 - 3.0), 6.0, 6.0);
			Line(d, ink, 1.0, num, num2 - 3.0, num, num2 - 7.0);
			Line(d, ink, 1.0, num, num2 - 3.0, num + 3.0, num2 - 2.0);
			break;
		case "confetti":
		{
			Box(d, wood, num - 4.0, num2 - 2.0, 8.0, 7.0, 1.0);
			for (int i = 0; i < 8; i++)
			{
				double num3 = Math.Max(0.0, Math.Sin(Math.Min(Math.PI, (elapsed - 2.0) * Math.PI / 2.0))) * 10.0;
				Box(d, (i % 2 == 0) ? cyan : shell, num - 3.0 + (double)(i % 4 * 2) + (double)(i - 4) * num3 * 0.2, num2 - 3.0 - (double)(i / 4 * 2) - num3, 1.5, 2.0, 0.0);
			}
			break;
		}
		}
	}

	private static Brush Fill(string c)
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(c));
		solidColorBrush.Freeze();
		return solidColorBrush;
	}

	private Point Hand()
	{
		double num = 1.12;
		double num2 = (0.0 - handRight) * Math.PI / 180.0;
		double num3 = 7.0 * num;
		return new Point(handX + 7.0 * num - num3 * Math.Sin(num2), 38.5 - 11.0 * num + num3 * Math.Cos(num2) + handJump);
	}

	private void DrawProps(DrawingContext d)
	{
		PetArt.SW = Math.Max(1.9, Math.Min(3.4, 1.8 / Math.Max(0.3, unit)));
		if (moment != null)
		{
			d.PushTransform(Legacy());
			DrawReactionProp(d);
			d.Pop();
		}
		string text = ((scene == null) ? "" : scene.Name);
		if (!text.StartsWith("moment-", StringComparison.Ordinal) && text != "first-meet")
		{
			DrawSurroundings(d);
		}
		if (text.StartsWith("bring-", StringComparison.Ordinal) && incoming != null)
		{
			DrawActivity(d, incoming, Value("act.x", 340.0), live: false);
		}
		else if (text.StartsWith("swap-", StringComparison.Ordinal))
		{
			if (activity != null)
			{
				DrawActivity(d, activity, Value("act.x", 340.0), live: false);
			}
			if (incoming != null)
			{
				DrawActivity(d, incoming, Value("next.x", 340.0), live: false);
			}
		}
		else if (text.StartsWith("away-", StringComparison.Ordinal) || text.StartsWith("moment-", StringComparison.Ordinal))
		{
			if (activity != null)
			{
				DrawActivity(d, activity, Value("act.x", 340.0), live: false);
			}
		}
		else if (text.StartsWith("done-", StringComparison.Ordinal) && activity != null)
		{
			DrawActivity(d, activity, 184.0, live: false);
			double num = Value("burst", 0.0);
			if (num > 0.0)
			{
				DoneBurst(d, P(184.0), num);
			}
		}
		else if (activity != null && (scene == null || text == "exit"))
		{
			DrawActivity(d, activity, (text == "exit") ? Value("act.x", 184.0) : 184.0, scene == null);
		}
		if (introBox && scene != null && !scene.Tracks.ContainsKey("box.x"))
		{
			BoxBack(d);
		}
		if (pillow || text == "pillow" || text == "return-pillow")
		{
			double num2 = P((pillow && text != "return-pillow") ? x : Value("pillow.x", -60.0));
			if (num2 > -30.0)
			{
				Pillow(d, num2);
			}
		}
		if (scene != null)
		{
			DrawScene(d, text);
		}
		else
		{
			DrawMicroFx(d);
		}
	}

	private void DrawActivity(DrawingContext d, PetActivity act, double ax, bool live)
	{
		double num = P(ax);
		if (num < -80.0 || num > P(340.0) + 80.0)
		{
			return;
		}
		PetLife l = L;
		bool flag = live && l.ActivityMotion;
		double num2 = (flag ? l.ActionPhase : 0.0);
		double num3 = (flag ? l.ActionTime : 0.0);
		double num4 = (flag ? Math.Min(1.0, num3 / l.ActionDuration) : 0.0);
		string text = (flag ? l.Action : null);
		double num5 = P(184.0);
		bool settled = Math.Abs(num - num5) < 0.5;
		double r = P(x);
		switch (act.Prop)
		{
		case "book":
			Book(d, num, (text == "page") ? num4 : 0.0);
			break;
		case "laptop":
			Laptop(d, num, text == "type", num4, num3);
			break;
		case "chai":
			Chai(d, num, (text == "sip") ? num2 : 0.0, num3, text != null);
			break;
		case "headphones":
			Boombox(d, num, (text == "nod" || text == "hum") ? num2 : 0.0, num3);
			break;
		case "rod":
			Fishing(d, num, settled, (text == "reel") ? num2 : 0.0, num3);
			break;
		case "telescope":
			Telescope(d, num, (text == "peer") ? num2 : 0.0, (text == "peer") ? num4 : 0.0, num3);
			break;
		case "pillow":
			Napping(d, num, r, (text == "snore") ? num4 : 0.0);
			break;
		case "gadget":
			Tinkering(d, num, settled, (text == "hammer" || text == "tap-gadget") ? num2 : 0.0, num3);
			break;
		case "can":
			Gardening(d, num, settled, (text == "water") ? num2 : 0.0, num3);
			break;
		case "notebook":
			Notebook(d, num, (text == "write") ? num4 : (-1.0), num3);
			break;
		case "popcorn":
			Popcorn(d, num, (text == "munch") ? num4 : (-1.0), (text == "watch") ? num2 : 0.0);
			break;
		case "easel":
			Painting(d, num, settled, (text == "brush") ? num4 : (-1.0), num3);
			break;
		case "cube":
			Puzzle(d, num, (text == "twist") ? num4 : 0.0, num3);
			break;
		case "yarn":
			Knitting(d, num, (text == "knit") ? num2 : 0.0, num3);
			break;
		case "mat":
			Yoga(d, num, r, (text == "breathe-deep" || text == "pose") ? num2 : 0.0);
			break;
		case "paper":
			Origami(d, num, (text == "fold") ? num2 : 0.0);
			break;
		case "table":
			TeaTable(d, num, (text == "sip") ? num2 : 0.0, num3, 1.0, 0.0, 0.0, 0.0, 0.0);
			break;
		case "plane":
			PlaneRest(d, num, settled, (text == "fold") ? num4 : (-1.0));
			break;
		case "umbrella":
			UmbrellaRain(d, num, settled, (text == "twirl") ? num2 : 0.0, num3);
			break;
		case "board":
			ChalkBoard(d, num, DrawingNow ?? "smiley", 1.0, 0.0, 0.0);
			if (text == "chalk-tap")
			{
				ChalkDust(d, num, num4);
			}
			break;
		case "ball":
			BallPlay(d, num, (text == "bounce") ? num4 : (-1.0));
			break;
		case "bucket":
			Bucket(d, num, OutcomeNow, (text == "flap") ? num2 : 0.0, num3);
			break;
		case "kite":
			KiteFly(d, num, settled, (text == "tug") ? num2 : 0.0, num3);
			break;
		case "bat":
			Cricket(d, num, settled, (text == "swing-bat") ? num2 : 0.0);
			break;
		case "guitar":
			Guitar(d, num - 34.0, (text == "strum") ? num2 : 0.0, num3, (text == "strum" || text == "hum") ? 1 : 0, 0.0);
			break;
		case "wand":
			Bubbles(d, num, settled, (text != null) ? num3 : 0.0);
			break;
		case "hat":
			MagicHat(d, num - 20.0, settled, (text == "wand") ? num2 : 0.0, num3);
			break;
		case "yoyo":
			Yoyo(d, num, settled, (text == "yoyo") ? num4 : (-1.0));
			break;
		case "bin":
			Bin(d, num, settled, (text == "toss") ? num4 : (-1.0));
			break;
		case "dominoes":
			Dominoes(d, num, 0.0, (text == "place") ? num4 : (-1.0));
			break;
		}
	}

	private static void Book(DrawingContext d, double a, double u)
	{
		double num = a - 4.0;
		PetArt.Rect(d, PetArt.Red, num - 31.0, 9.0, 62.0, 29.5, 3.0);
		PetArt.Poly(d, PetArt.White, null, num - 28.0, 11.0, num - 2.0, 14.0, num - 2.0, 35.5, num - 28.0, 33.5);
		PetArt.Poly(d, PetArt.White, null, num + 2.0, 14.0, num + 28.0, 11.0, num + 28.0, 33.5, num + 2.0, 35.5);
		for (int i = 0; i < 4; i++)
		{
			double num2 = 18.0 + (double)i * 4.6;
			double num3 = ((i == 3) ? (-14) : (-6));
			PetArt.Line(d, PetArt.Ink, num - 24.0, num2 - 1.4, num + num3, num2 + 0.6 - ((i == 3) ? 0.5 : 0.0), PetArt.SW * 0.85);
			PetArt.Line(d, PetArt.Ink, num + 6.0, num2 + 0.6, num + (double)((i == 3) ? 16 : 24), num2 - 1.4 + ((i == 3) ? 0.6 : 0.0), PetArt.SW * 0.85);
		}
		if (u > 0.0 && u < 1.0)
		{
			double num4 = 26.0 * Math.Cos(Math.PI * u);
			double num5 = 6.0 * Math.Sin(Math.PI * u);
			if (Math.Abs(num4) < 1.5)
			{
				PetArt.Line(d, PetArt.White, num, 13.0, num, 35.5, PetArt.SW * 1.2);
				return;
			}
			PetArt.Poly(d, PetArt.White, PetArt.Ink, num, 14.0, num + num4, 11.0 - num5, num + num4, 33.5 - num5, num, 35.5);
		}
	}

	private static void Laptop(DrawingContext d, double a, bool typing, double u, double t)
	{
		double num = a - 8.0;
		PetArt.Rect(d, PetArt.White, num - 28.0, 3.0, 56.0, 31.0, 3.5);
		PetArt.Rect(d, (typing && (int)(t * 7.0) % 2 == 0) ? CyanBright : PetArt.Cyan, num - 24.0, 6.5, 48.0, 24.0, 1.5);
		double[] array = new double[4] { 30.0, 20.0, 34.0, 0.0 };
		double[] array2 = new double[4] { 0.0, 6.0, 6.0, 0.0 };
		double num2 = (typing ? (4.0 + 24.0 * u) : 16.0);
		for (int i = 0; i < 4; i++)
		{
			double num3 = 11.0 + (double)i * 5.4;
			double num4 = ((i == 3) ? num2 : array[i]);
			PetArt.Line(d, PetArt.Ink, num - 20.0 + array2[i], num3, num - 20.0 + array2[i] + num4, num3, PetArt.SW);
		}
		if (!typing || (int)(t * 6.0) % 2 == 0)
		{
			PetArt.Rect(d, PetArt.Ink, num - 20.0 + num2 + 2.2, 24.800000000000004, 3.0, 4.8, 0.6);
		}
		PetArt.Poly(d, PetArt.White, null, num - 35.0, 38.5, num + 35.0, 38.5, num + 29.0, 34.0, num - 29.0, 34.0);
		for (int j = 0; j < 6; j++)
		{
			PetArt.Line(d, PetArt.Ink, num - 24.0 + (double)j * 9.4, 36.3, num - 19.6 + (double)j * 9.4, 36.3, PetArt.SW * 0.8);
		}
	}

	private static void Chai(DrawingContext d, double a, double sip, double t, bool live)
	{
		double num = a - 30.0;
		PetArt.Oval(d, PetArt.Wood, num - 2.0, 37.0, 20.0, 2.6);
		d.PushTransform(new TranslateTransform(0.0, -3.0 * sip));
		d.PushTransform(new RotateTransform(-16.0 * sip, num - 15.0, 36.0));
		PetArt.Rect(d, PetArt.White, num - 15.0, 13.0, 26.0, 24.0, 5.0);
		PetArt.Rect(d, PetArt.Red, num - 15.0, 23.0, 26.0, 5.0);
		PetArt.Oval(d, PetArt.Wood, num - 2.0, 14.6, 11.0, 2.4);
		PetArt.Ring(d, PetArt.White, num + 14.0, 24.5, 5.5, PetArt.SW * 1.3);
		d.Pop();
		d.Pop();
		for (int i = 0; i < 3; i++)
		{
			double num2 = num - 9.0 + (double)(i * 7) + (live ? (Math.Sin(t * 5.0 + (double)i) * 1.6) : 0.0);
			double num3 = 10.5 - 3.0 * sip;
			PetArt.Curve(d, PetArt.White, PetArt.SW * 0.9, num2, num3, num2 - 3.4, num3 - 2.6, num2, num3 - 5.2);
			PetArt.Curve(d, PetArt.White, PetArt.SW * 0.9, num2, num3 - 5.2, num2 + 3.4, num3 - 7.8, num2, num3 - 10.4);
		}
		double num4 = a + 24.0;
		PetArt.Oval(d, PetArt.White, num4, 29.0, 13.5, 9.5);
		PetArt.Rect(d, PetArt.Red, num4 - 7.0, 17.5, 14.0, 3.6, 1.5);
		PetArt.Dot(d, PetArt.Red, num4, 16.3, 2.6);
		PetArt.Poly(d, PetArt.White, null, num4 - 10.0, 26.0, num4 - 19.0, 19.0, num4 - 16.0, 17.6, num4 - 7.0, 23.0);
		PetArt.Ring(d, PetArt.White, num4 + 14.5, 28.0, 5.0, PetArt.SW * 1.3);
	}

	private static void Boombox(DrawingContext d, double a, double beat, double t)
	{
		PetArt.Polyline(d, PetArt.White, PetArt.SW * 1.3, a - 15.0, 13.0, a - 15.0, 7.5, a + 15.0, 7.5, a + 15.0, 13.0);
		PetArt.Rect(d, PetArt.White, a - 31.0, 12.5, 62.0, 26.0, 4.5);
		double num = 1.8 * beat * Math.Abs(Math.Sin(t * 10.0));
		double[] array = new double[2]
		{
			a - 16.0,
			a + 16.0
		};
		foreach (double num2 in array)
		{
			PetArt.Dot(d, PetArt.Ink, num2, 26.0, 9.0);
			PetArt.Dot(d, PetArt.Cyan, num2, 26.0, 5.8 + num);
			PetArt.Dot(d, PetArt.Ink, num2, 26.0, 2.2);
		}
		PetArt.Rect(d, PetArt.Ink, a - 5.5, 15.5, 11.0, 5.0, 1.2);
		double num3 = 4.0 * beat;
		PetArt.Note(d, PetArt.Yellow, a + 43.0, 23.0 - num3, 11.0);
		PetArt.Note(d, PetArt.Yellow, a - 44.0, 17.0 + num3 * 0.6, 9.0);
		PetArt.Note(d, PetArt.Yellow, a + 53.0, 12.0 + num3 * 0.4, 7.0);
	}

	private void Fishing(DrawingContext d, double a, bool settled, double reel, double t)
	{
		double num = a - 62.0;
		double num2 = a + 52.0;
		PetArt.Rect(d, PetArt.Cyan, num, 33.0, num2 - num, 6.5, 2.5);
		for (double num3 = num + 6.0; num3 < num2 - 8.0; num3 += 16.0)
		{
			PetArt.Line(d, PetArt.White, num3, 34.7, num3 + 6.0, 34.7, PetArt.SW * 0.8);
		}
		Point point = Hand();
		Point point2 = (settled ? point : new Point(a - 78.0, 27.0));
		Point point3 = new Point(point2.X + 34.0, 6.0 - 3.0 * reel);
		PetArt.Line(d, PetArt.Wood, point2.X, point2.Y, point3.X, point3.Y, PetArt.SW * 1.3);
		PetArt.Dot(d, PetArt.White, point2.X + (point3.X - point2.X) * 0.14, point2.Y + (point3.Y - point2.Y) * 0.14, 2.6);
		double num4 = 3.2 * Math.Max(0.0, Math.Sin(t * 5.0)) * reel;
		double num5 = point3.X + 18.0;
		double num6 = 31.5 + num4;
		PetArt.Line(d, PetArt.White, point3.X, point3.Y, num5, num6 - 3.6, PetArt.SW * 0.7);
		Bobber(d, num5, num6);
		if (num4 > 0.6)
		{
			d.DrawEllipse(null, PetArt.Pen(PetArt.White, PetArt.SW * 0.7), new Point(num5, 35.2), 6.0 + num4 * 2.0, 1.9);
		}
	}

	private static void Bobber(DrawingContext d, double bx, double by)
	{
		PetArt.Dot(d, PetArt.White, bx, by, 3.8);
		d.PushClip(new RectangleGeometry(new Rect(bx - 5.0, by - 5.0, 10.0, 5.0)));
		PetArt.Dot(d, PetArt.Red, bx, by, 3.8);
		d.Pop();
	}

	private void Telescope(DrawingContext d, double a, double peer, double u, double t)
	{
		Jar(d, a + 42.0);
		double num = a - 24.0;
		PetArt.Line(d, PetArt.Wood, num, 21.0, num - 11.0, 38.5, PetArt.SW * 1.1);
		PetArt.Line(d, PetArt.Wood, num, 21.0, num + 10.0, 38.5, PetArt.SW * 1.1);
		PetArt.Line(d, PetArt.Wood, num, 21.0, num, 38.5, PetArt.SW * 1.1);
		d.PushTransform(new RotateTransform(-28.0 - 6.0 * peer, num, 21.0));
		PetArt.Rect(d, PetArt.White, num - 20.0, 16.5, 44.0, 9.0, 3.0);
		PetArt.Rect(d, PetArt.Red, num + 4.0, 16.5, 4.5, 9.0);
		PetArt.Rect(d, PetArt.White, num - 27.0, 18.0, 7.5, 6.0, 1.5);
		PetArt.Oval(d, PetArt.Cyan, num + 24.0, 21.0, 2.4, 4.6);
		d.Pop();
		double num2 = 1.0 + 0.4 * Math.Sin(t * 9.0) * peer;
		PetArt.Star(d, PetArt.Yellow, a + 12.0, 6.0, 3.6 * num2, 4, 0.38);
		PetArt.Star(d, PetArt.Yellow, a + 28.0, 13.0, 2.8, 4, 0.38);
		if (u > 0.3)
		{
			double num3 = (u - 0.3) / 0.7;
			PetArt.Star(d, PetArt.Yellow, a + 12.0 + 30.0 * num3, 6.0 + 16.0 * num3, 3.2);
		}
	}

	private void Jar(DrawingContext d, double jx)
	{
		int starJar = StarJar;
		PetArt.Rect(d, PetArt.Wood, jx - 10.0, 14.5, 20.0, 4.5, 1.5);
		d.DrawRoundedRectangle(Glass, PetArt.Pen(PetArt.White, PetArt.SW * 0.9), new Rect(jx - 9.0, 18.5, 18.0, 20.0), 4.0, 4.0);
		for (int i = 0; i < Math.Min(21, starJar); i++)
		{
			PetArt.Star(d, PetArt.Yellow, jx - 6.4 + (double)(i % 5) * 3.2, 35.6 - (double)(i / 5) * 3.6, 2.1, 4);
		}
	}

	private static void Napping(DrawingContext d, double a, double r, double u)
	{
		PetArt.Rect(d, PetArt.White, r - 23.0, 31.0, 46.0, 8.5, 4.2);
		PetArt.Line(d, PetArt.Ink, r - 17.0, 35.2, r + 17.0, 35.2, PetArt.SW * 0.8);
		PetArt.Dot(d, PetArt.Yellow, a + 30.0, 17.0, 12.0);
		PetArt.Dot(d, Brushes.Black, a + 36.5, 12.5, 10.6);
		PetArt.Star(d, PetArt.Yellow, a + 48.0, 30.0, 2.6, 4, 0.4);
		PetArt.Star(d, PetArt.White, a + 8.0, 8.0, 2.4, 4, 0.4);
		PetArt.Zee(d, a - 36.0, 24.0 - 5.0 * u, 6.0);
		PetArt.Zee(d, a - 22.0, 14.0 - 5.0 * u, 8.5);
		PetArt.Zee(d, a - 5.0, 3.0 - 2.0 * u, 10.5);
	}

	private static void Rocket(DrawingContext d, double cx, double ground, int parts, double flame, double smoke)
	{
		if (smoke > 0.0)
		{
			d.PushOpacity(Math.Min(1.0, smoke));
			for (int i = 0; i < 4; i++)
			{
				double num = 4.0 + smoke * 3.0 + (double)i;
				PetArt.Oval(d, PetArt.White, cx + ((double)i - 1.5) * 9.0 * (1.0 + smoke * 0.3), 36.5, num, num * 0.6);
			}
			d.Pop();
		}
		PetArt.Rect(d, PetArt.Wood, cx - 16.0, 35.0, 32.0, 3.5, 1.5);
		if (flame > 0.0)
		{
			PetArt.Poly(d, PetArt.Orange, null, cx - 6.0, ground - 4.0, cx + 6.0, ground - 4.0, cx, ground + 10.0 * flame);
			PetArt.Poly(d, PetArt.Yellow, null, cx - 3.0, ground - 4.0, cx + 3.0, ground - 4.0, cx, ground + 5.0 * flame);
		}
		if (parts >= 1)
		{
			PetArt.Poly(d, PetArt.White, null, cx - 5.0, ground - 8.0, cx + 5.0, ground - 8.0, cx + 7.0, ground - 3.5, cx - 7.0, ground - 3.5);
		}
		if (parts >= 2)
		{
			PetArt.Rect(d, PetArt.White, cx - 8.0, ground - 28.0, 16.0, 21.0, 6.0);
		}
		if (parts >= 3)
		{
			PetArt.Dot(d, PetArt.Cyan, cx, ground - 20.0, 3.8, PetArt.Ink);
		}
		if (parts >= 4)
		{
			PetArt.Poly(d, PetArt.Red, null, cx - 8.0, ground - 25.0, cx, ground - 36.0, cx + 8.0, ground - 25.0);
		}
		if (parts >= 5)
		{
			PetArt.Poly(d, PetArt.Red, null, cx - 8.0, ground - 16.0, cx - 15.0, ground - 5.0, cx - 8.0, ground - 8.0);
			PetArt.Poly(d, PetArt.Red, null, cx + 8.0, ground - 16.0, cx + 15.0, ground - 5.0, cx + 8.0, ground - 8.0);
		}
		if (parts >= 6)
		{
			PetArt.Rect(d, PetArt.Red, cx - 8.0, ground - 13.0, 16.0, 3.0);
		}
		if (parts >= 7)
		{
			PetArt.Dot(d, PetArt.Yellow, cx - 4.0, ground - 5.8, 1.6);
			PetArt.Dot(d, PetArt.Yellow, cx + 4.0, ground - 5.8, 1.6);
		}
		if (parts <= 0)
		{
			PetArt.Line(d, PetArt.Wood, cx - 10.0, 35.0, cx - 10.0, 16.5, PetArt.SW);
			PetArt.Line(d, PetArt.Wood, cx + 10.0, 35.0, cx + 10.0, 16.5, PetArt.SW);
			PetArt.Line(d, PetArt.Wood, cx - 10.0, 24.5, cx + 10.0, 24.5, PetArt.SW);
		}
	}

	private void Tinkering(DrawingContext d, double a, bool settled, double tap, double t)
	{
		double num = a - 40.0;
		Rocket(d, num, 38.5, RocketDay, 0.0, 0.0);
		double num2 = a + 24.0;
		PetArt.Line(d, PetArt.Wood, num2 - 10.0, 33.0, num2 - 13.0, 38.5, PetArt.SW);
		PetArt.Line(d, PetArt.Wood, num2 + 10.0, 33.0, num2 + 13.0, 38.5, PetArt.SW);
		PetArt.Rect(d, PetArt.Cyan, num2 - 17.0, 6.0, 34.0, 28.0, 2.0);
		PetArt.Polyline(d, PetArt.White, PetArt.SW * 0.85, num2, 9.0, num2 + 5.0, 15.0, num2 + 5.0, 27.0, num2 - 5.0, 27.0, num2 - 5.0, 15.0, num2, 9.0);
		PetArt.Line(d, PetArt.White, num2 - 5.0, 27.0, num2 - 9.0, 31.0, PetArt.SW * 0.85);
		PetArt.Line(d, PetArt.White, num2 + 5.0, 27.0, num2 + 9.0, 31.0, PetArt.SW * 0.85);
		PetArt.Ring(d, PetArt.White, num2, 19.0, 2.4, PetArt.SW * 0.7);
		Point point = Hand();
		Point point2 = new Point(num - 12.0, 22.0);
		if (!settled)
		{
			point = new Point(num - 50.0, 27.0);
		}
		double num3 = Math.Abs(Math.Sin(t * 6.0)) * tap;
		Point point3 = new Point(point2.X - 3.0 * num3, point2.Y - 12.0 * num3);
		PetArt.Line(d, PetArt.Wood, point.X, point.Y, point3.X, point3.Y, PetArt.SW * 1.2);
		double angle = Math.Atan2(point3.Y - point.Y, point3.X - point.X) * 180.0 / Math.PI;
		d.PushTransform(new RotateTransform(angle, point3.X, point3.Y));
		PetArt.Rect(d, PetArt.White, point3.X - 2.5, point3.Y - 6.0, 6.0, 12.0, 1.5);
		d.Pop();
		if (tap > 0.3 && num3 < 0.25)
		{
			PetArt.Star(d, PetArt.Yellow, num - 9.0, 21.0, 5.0, 4, 0.32);
		}
	}

	private void Gardening(DrawingContext d, double a, bool settled, double water, double t)
	{
		double num = a - 18.0;
		Plant(d, num, PetStories.PlantStage(PlantDay));
		PetArt.Dot(d, PetArt.Yellow, a + 34.0, 11.0, 6.5);
		for (int i = 0; i < 8; i++)
		{
			double num2 = (double)i * Math.PI / 4.0;
			PetArt.Line(d, PetArt.Yellow, a + 34.0 + Math.Cos(num2) * 9.0, 11.0 + Math.Sin(num2) * 9.0, a + 34.0 + Math.Cos(num2) * 12.5, 11.0 + Math.Sin(num2) * 12.5, PetArt.SW * 0.9);
		}
		Point point = (settled ? Hand() : new Point(a - 74.0, 24.0));
		Point point2 = new Point(point.X + 11.0, point.Y - 7.0);
		d.PushTransform(new RotateTransform(14.0 + 16.0 * water, point2.X, point2.Y));
		PetArt.Rect(d, PetArt.Cyan, point2.X - 10.0, point2.Y - 8.0, 20.0, 16.0, 3.5);
		PetArt.Ring(d, PetArt.Cyan, point2.X - 10.0, point2.Y - 2.0, 5.0, PetArt.SW * 1.2);
		PetArt.Line(d, PetArt.Cyan, point2.X + 8.0, point2.Y - 1.0, point2.X + 26.0, point2.Y - 10.0, PetArt.SW * 1.5);
		PetArt.Oval(d, PetArt.Cyan, point2.X + 27.5, point2.Y - 10.5, 2.6, 3.6);
		d.Pop();
		double num3 = (14.0 + 16.0 * water) * Math.PI / 180.0;
		double num4 = point2.X + 28.0 * Math.Cos(num3) + 10.5 * Math.Sin(num3);
		double num5 = point2.Y + 28.0 * Math.Sin(num3) - 10.5 * Math.Cos(num3);
		int num6 = ((!(water > 0.05)) ? 1 : 3);
		for (int j = 0; j < num6; j++)
		{
			double num7 = ((water > 0.05) ? ((t * 2.2 + (double)j / 3.0) % 1.0) : 0.25);
			double num8 = num4 + (num - num4) * num7 * 0.9;
			double y = num5 + (20.0 - num5) * num7 + 6.0 * num7 * num7;
			PetArt.Drop(d, PetArt.Cyan, num8, y, 2.8);
		}
	}

	private static void Plant(DrawingContext d, double px, int stage)
	{
		PetArt.Poly(d, PetArt.Wood, null, px - 12.0, 27.0, px + 12.0, 27.0, px + 9.0, 38.5, px - 9.0, 38.5);
		PetArt.Rect(d, PetArt.White, px - 14.0, 24.5, 28.0, 4.5, 1.5);
		if (stage <= 0)
		{
			PetArt.Oval(d, PetArt.Green, px, 23.5, 4.0, 2.0);
			return;
		}
		double num = stage switch
		{
			2 => 10, 
			1 => 15, 
			_ => 7, 
		};
		PetArt.Line(d, PetArt.Green, px, 24.5, px, num, PetArt.SW * 1.2);
		Leaf(d, px, num + 3.0, -35.0, 7.0);
		Leaf(d, px, num + 3.0, 215.0, 7.0);
		if (stage >= 2)
		{
			Leaf(d, px, num + 9.0, -20.0, 8.5);
			Leaf(d, px, num + 9.0, 200.0, 8.5);
		}
		if (stage == 3)
		{
			PetArt.Oval(d, PetArt.Pink, px, num - 1.5, 2.8, 3.8);
		}
		if (stage >= 4)
		{
			for (int i = 0; i < 5; i++)
			{
				double num2 = -Math.PI / 2.0 + (double)i * Math.PI * 2.0 / 5.0;
				PetArt.Dot(d, PetArt.Pink, px + Math.Cos(num2) * 3.6, num - 1.5 + Math.Sin(num2) * 3.6, 3.0);
			}
			PetArt.Dot(d, PetArt.Yellow, px, num - 1.5, 2.4);
		}
	}

	private static void Leaf(DrawingContext d, double x, double y, double angle, double len)
	{
		d.PushTransform(new RotateTransform(angle, x, y));
		PetArt.Oval(d, PetArt.Green, x + len / 2.0, y, len / 2.0, len * 0.27);
		d.Pop();
	}

	private static void Notebook(DrawingContext d, double a, double u, double t)
	{
		double num = a - 12.0;
		PetArt.Rect(d, PetArt.White, num - 24.0, 5.0, 48.0, 33.5, 2.5);
		for (int i = 0; i < 6; i++)
		{
			double num2 = num - 18.0 + (double)i * 7.2;
			PetArt.Ring(d, PetArt.White, num2, 4.6, 2.4, PetArt.SW * 0.8);
			PetArt.Dot(d, PetArt.Ink, num2, 7.4, 1.4);
		}
		for (int j = 0; j < 5; j++)
		{
			PetArt.Line(d, PetArt.Cyan, num - 20.0, 13 + j * 5, num + 20.0, 13 + j * 5, PetArt.SW * 0.6);
		}
		double num3 = ((u < 0.0) ? 0.55 : u);
		Point point = new Point(num - 18.0, 22.0);
		for (int k = 0; k < 3; k++)
		{
			double num4 = 11.6 + (double)(k * 5);
			double num5 = ((k < 2) ? ((double)((k == 0) ? 34 : 28)) : (26.0 * num3));
			if (!(num5 < 1.0))
			{
				List<double> list = new List<double>();
				for (double num6 = 0.0; num6 <= num5; num6 += 2.6)
				{
					list.Add(num - 18.0 + num6);
					list.Add(num4 + (((int)(num6 / 2.6) % 2 == 0) ? (-1.3) : 1.3));
				}
				if (list.Count >= 4)
				{
					PetArt.Polyline(d, PetArt.Ink, PetArt.SW * 0.8, list.ToArray());
				}
				point = new Point(num - 18.0 + num5, num4);
			}
		}
		double num7 = a + 30.0;
		PetArt.Poly(d, PetArt.Cyan, null, num7 - 8.0, 22.0, num7 + 8.0, 22.0, num7 + 6.5, 38.5, num7 - 6.5, 38.5);
		PetArt.Line(d, PetArt.Yellow, num7 - 4.0, 22.0, num7 - 6.0, 11.0, PetArt.SW * 1.3);
		PetArt.Line(d, PetArt.Red, num7 + 1.0, 22.0, num7 + 1.0, 9.0, PetArt.SW * 1.3);
		PetArt.Line(d, PetArt.Yellow, num7 + 5.0, 22.0, num7 + 8.0, 12.0, PetArt.SW * 1.3);
		double num8 = ((u < 0.0) ? 0.0 : (Math.Sin(t * 14.0) * 0.8));
		d.PushTransform(new RotateTransform(-52.0, point.X, point.Y + num8));
		PetArt.Poly(d, PetArt.Wood, null, point.X, point.Y + num8, point.X + 3.4, point.Y + num8 - 2.4, point.X + 3.4, point.Y + num8 + 2.4);
		PetArt.Rect(d, PetArt.Yellow, point.X + 3.2, point.Y + num8 - 2.4, 17.0, 4.8, 1.0);
		PetArt.Rect(d, PetArt.Pink, point.X + 20.0, point.Y + num8 - 2.4, 4.5, 4.8, 1.2);
		d.Pop();
	}

	private static void Popcorn(DrawingContext d, double a, double u, double watch)
	{
		double num = a - 28.0;
		PetArt.Poly(d, PetArt.White, null, num - 15.0, 16.0, num + 15.0, 16.0, num + 11.0, 38.5, num - 11.0, 38.5);
		double[] array = new double[3] { -8.5, 0.0, 8.5 };
		foreach (double num2 in array)
		{
			PetArt.Poly(d, PetArt.Red, null, num + num2 - 2.6, 16.0, num + num2 + 2.6, 16.0, num + num2 * 0.73 + 1.9, 38.5, num + num2 * 0.73 - 1.9, 38.5);
		}
		Point[] array2 = new Point[6]
		{
			new Point(num - 10.0, 15.0),
			new Point(num - 3.0, 12.0),
			new Point(num + 5.0, 13.0),
			new Point(num + 11.0, 15.5),
			new Point(num + 1.0, 8.5),
			new Point(num - 7.0, 9.5)
		};
		for (int j = 0; j < array2.Length; j++)
		{
			Point point = array2[j];
			PetArt.Dot(d, PetArt.White, point.X, point.Y, 4.4);
		}
		PetArt.Dot(d, PetArt.Yellow, num - 2.0, 11.0, 1.6);
		PetArt.Dot(d, PetArt.Yellow, num + 8.0, 13.5, 1.5);
		if (u >= 0.0)
		{
			for (int k = 0; k < 2; k++)
			{
				double num3 = (u * 1.6 + (double)k * 0.5) % 1.0;
				PetArt.Dot(d, PetArt.White, num - 4.0 + (double)(k * 9) + 10.0 * num3 * (double)((k != 0) ? 1 : (-1)), 8.0 - 16.0 * num3 * (1.0 - num3) * 2.4, 3.2);
			}
		}
		double num4 = a + 26.0;
		PetArt.Rect(d, PetArt.White, num4 - 20.0, 7.0, 40.0, 27.0, 3.0);
		PetArt.Rect(d, (watch > 0.0 && Math.Sin(watch * 20.0) > 0.0) ? CyanBright : PetArt.Cyan, num4 - 16.5, 10.5, 33.0, 20.0, 1.5);
		PetArt.Poly(d, PetArt.White, null, num4 - 4.0, 15.0, num4 + 6.0, 20.5, num4 - 4.0, 26.0);
		PetArt.Line(d, PetArt.White, num4, 34.0, num4, 37.0, PetArt.SW * 1.2);
		PetArt.Line(d, PetArt.White, num4 - 9.0, 38.5, num4 + 9.0, 38.5, PetArt.SW * 1.3);
	}

	private void Painting(DrawingContext d, double a, bool settled, double u, double t)
	{
		double num = a - 36.0;
		PetArt.Line(d, PetArt.Wood, num, 3.0, num, 38.5, PetArt.SW * 1.1);
		PetArt.Line(d, PetArt.Wood, num - 12.0, 28.0, num - 17.0, 38.5, PetArt.SW * 1.1);
		PetArt.Line(d, PetArt.Wood, num + 12.0, 28.0, num + 17.0, 38.5, PetArt.SW * 1.1);
		PetArt.Rect(d, PetArt.White, num - 19.0, 4.0, 38.0, 27.0, 1.5);
		PetArt.Poly(d, PetArt.Cyan, null, num - 17.0, 29.0, num - 17.0, 22.0, num - 8.0, 17.0, num + 2.0, 22.0, num + 10.0, 18.5, num + 17.0, 22.0, num + 17.0, 29.0);
		PetArt.Dot(d, PetArt.Yellow, num + 7.0, 11.5, 4.6);
		int num2 = ((u < 0.0) ? 5 : (1 + (int)(u * 5.0)));
		for (int i = 0; i < num2; i++)
		{
			double num3 = Math.PI + (double)i * Math.PI / 4.0;
			PetArt.Line(d, PetArt.Yellow, num + 7.0 + Math.Cos(num3) * 6.6, 11.5 + Math.Sin(num3) * 6.6, num + 7.0 + Math.Cos(num3) * 9.6, 11.5 + Math.Sin(num3) * 9.6, PetArt.SW * 0.9);
		}
		Point point = (settled ? Hand() : new Point(num - 56.0, 25.0));
		double num4 = ((u < 0.0) ? 0.0 : (Math.Sin(t * 5.0) * 3.0));
		Point point2 = new Point(num - 9.0, 10.0 + num4);
		PetArt.Line(d, PetArt.White, point.X, point.Y, point2.X - 3.0, point2.Y + 1.5, PetArt.SW * 1.1);
		PetArt.Line(d, PetArt.Yellow, point2.X - 3.5, point2.Y + 1.7, point2.X, point2.Y, PetArt.SW * 1.8);
		double num5 = a + 24.0;
		PetArt.Oval(d, PetArt.White, num5, 28.0, 15.0, 9.0);
		PetArt.Dot(d, PetArt.Yellow, num5 - 7.0, 26.0, 3.2);
		PetArt.Dot(d, PetArt.Cyan, num5 + 1.0, 24.5, 3.2);
		PetArt.Dot(d, Brushes.Black, num5 + 8.0, 30.5, 2.8);
	}

	private static void Puzzle(DrawingContext d, double a, double u, double t)
	{
		double num = a - 16.0;
		double num2 = num - 16.5;
		double num3 = 5.5;
		double num4 = 10.2;
		double num5 = 1.2;
		d.DrawRoundedRectangle(rectangle: new Rect(num2 - 1.5, num3 - 1.5, 36.0, 36.0), brush: Brushes.Black, pen: PetArt.Pen(PetArt.White, PetArt.SW * 0.8), radiusX: 3.0, radiusY: 3.0);
		double num6 = u * (num4 + num5);
		d.PushClip(new RectangleGeometry(new Rect(num2, num3, 33.0, 33.0)));
		for (int i = 0; i < 3; i++)
		{
			for (int j = -1; j < 3; j++)
			{
				if (i <= 0 || j >= 0)
				{
					int num7 = (j % 3 + 3) % 3;
					double num8 = ((i == 0) ? num6 : 0.0);
					PetArt.Rect(d, cubeColors[i * 3 + num7], num2 + (double)j * (num4 + num5) + num8 + 0.6, num3 + (double)i * (num4 + num5) + 0.6, num4 - 0.6, num4 - 0.6, 1.6);
				}
			}
		}
		d.Pop();
		double num9 = a + 32.0;
		PetArt.Rect(d, PetArt.White, num9 - 3.0, 10.0, 6.0, 4.0, 1.0);
		PetArt.Ring(d, PetArt.White, num9, 24.0, 9.5, PetArt.SW * 1.1);
		double num10 = t * 2.0;
		PetArt.Line(d, PetArt.White, num9, 24.0, num9 + Math.Sin(num10) * 6.5, 24.0 - Math.Cos(num10) * 6.5, PetArt.SW);
		PetArt.Dot(d, PetArt.Red, num9, 24.0, 2.0);
	}

	private void Knitting(DrawingContext d, double a, double knit, double t)
	{
		PetArt.Dot(d, PetArt.Pink, a + 20.0, 27.0, 11.5);
		PetArt.Curve(d, PetArt.White, PetArt.SW * 0.8, a + 11.0, 22.0, a + 20.0, 16.5, a + 29.0, 22.0);
		PetArt.Curve(d, PetArt.White, PetArt.SW * 0.8, a + 10.0, 29.0, a + 20.0, 34.0, a + 30.0, 27.5);
		Point point = new Point(P(x) + 24.0, 18.0);
		double num = 12.0 * Math.Sin(t * 9.0) * knit;
		double[] array = new double[2]
		{
			-35.0 + num,
			215.0 - num
		};
		foreach (double num2 in array)
		{
			double num3 = num2 * Math.PI / 180.0;
			PetArt.Line(d, PetArt.White, point.X - Math.Cos(num3) * 15.0, point.Y - Math.Sin(num3) * 15.0, point.X + Math.Cos(num3) * 15.0, point.Y + Math.Sin(num3) * 15.0, PetArt.SW);
			PetArt.Dot(d, PetArt.Yellow, point.X - Math.Cos(num3) * 15.0, point.Y - Math.Sin(num3) * 15.0, 2.6);
		}
		PetArt.Rect(d, PetArt.Pink, point.X - 6.5, point.Y + 3.0, 13.0, 20.0, 2.0);
		for (int j = 0; j < 4; j++)
		{
			PetArt.Polyline(d, PetArt.White, PetArt.SW * 0.7, point.X - 4.0, point.Y + 7.0 + (double)j * 4.4, point.X, point.Y + 9.0 + (double)j * 4.4, point.X + 4.0, point.Y + 7.0 + (double)j * 4.4);
		}
		PetArt.Curve(d, PetArt.Pink, PetArt.SW * 0.9, a + 9.0, 31.0, a - 12.0, 37.0, point.X + 6.0, point.Y + 8.0);
	}

	private static void Yoga(DrawingContext d, double a, double r, double breath)
	{
		PetArt.Rect(d, PetArt.Green, r - 30.0, 37.0, 60.0, 2.8, 1.2);
		PetArt.Curve(d, PetArt.White, PetArt.SW * 0.8, r + 20.0, 8.0 - 2.0 * breath, r + 25.0, 14.0, r + 20.0, 20.0);
		PetArt.Curve(d, PetArt.White, PetArt.SW * 0.8, r - 20.0, 8.0 - 2.0 * breath, r - 25.0, 14.0, r - 20.0, 20.0);
		PetArt.Rect(d, PetArt.Pink, a - 30.0, 28.0, 24.0, 10.5, 5.0);
		PetArt.Ring(d, PetArt.White, a - 24.0, 33.2, 3.0, PetArt.SW * 0.8);
		PetArt.Rect(d, PetArt.White, a - 2.0, 31.0, 14.0, 3.0, 1.2);
		PetArt.Rect(d, PetArt.Cyan, a - 5.0, 27.5, 5.0, 10.0, 1.5);
		PetArt.Rect(d, PetArt.Cyan, a + 9.0, 27.5, 5.0, 10.0, 1.5);
		PetArt.Rect(d, PetArt.Cyan, a + 22.0, 17.0, 12.0, 21.5, 3.5);
		PetArt.Rect(d, PetArt.White, a + 24.0, 13.0, 8.0, 5.0, 1.5);
		PetArt.Rect(d, PetArt.White, a + 22.0, 25.0, 12.0, 4.0);
	}

	private static void Origami(DrawingContext d, double a, double flap)
	{
		double num = a - 14.0;
		PetArt.Poly(d, PetArt.White, null, num + 2.0, 27.0, num + 22.0, 9.0, num + 13.0, 27.0);
		PetArt.Poly(d, PetArt.White, null, num - 5.0, 27.0, num - 19.0, 10.0, num - 14.0, 27.0);
		PetArt.Poly(d, PetArt.White, null, num - 19.0, 10.0, num - 25.0, 13.5, num - 17.5, 12.5);
		PetArt.Poly(d, PetArt.White, null, num - 11.0, 27.0, num + 11.0, 27.0, num + 5.0, 33.0, num - 5.0, 33.0);
		PetArt.Poly(d, PetArt.White, PetArt.Ink, num - 7.0, 25.0, num + 7.0, 25.0, num + 1.0, 5.0 - flap * 4.0);
		PetArt.Rect(d, PetArt.Wood, num - 18.0, 33.0, 36.0, 5.5, 2.0);
		d.PushTransform(new RotateTransform(-8.0, a + 30.0, 30.0));
		PetArt.Rect(d, PetArt.Yellow, a + 20.0, 22.0, 20.0, 16.0, 1.5);
		d.Pop();
		d.PushTransform(new RotateTransform(7.0, a + 32.0, 30.0));
		PetArt.Rect(d, PetArt.Pink, a + 22.0, 25.0, 20.0, 13.5, 1.5);
		d.Pop();
	}

	private static void TeaTable(DrawingContext d, double a, double sip, double t, double fill, double pour, double tip, double puddle, double heart)
	{
		double num = a - 26.0;
		d.PushTransform(new ScaleTransform(1.35, 1.35, num, 38.5));
		PetArt.Rect(d, PetArt.Wood, num - 27.0, 25.0, 54.0, 4.0, 1.5);
		PetArt.Line(d, PetArt.Wood, num - 20.0, 29.0, num - 22.0, 38.5, PetArt.SW * 1.3);
		PetArt.Line(d, PetArt.Wood, num + 20.0, 29.0, num + 22.0, 38.5, PetArt.SW * 1.3);
		double num2 = num + 13.0;
		d.PushTransform(new RotateTransform(-32.0 * pour, num2 + 8.0, 24.0));
		PetArt.Oval(d, PetArt.White, num2, 18.5, 9.0, 6.2);
		PetArt.Rect(d, PetArt.Red, num2 - 5.0, 10.8, 10.0, 3.0, 1.2);
		PetArt.Poly(d, PetArt.White, null, num2 - 8.0, 17.5, num2 - 15.0, 12.0, num2 - 13.0, 11.0, num2 - 5.0, 15.5);
		PetArt.Ring(d, PetArt.White, num2 + 9.0, 18.0, 3.6, PetArt.SW * 1.1);
		d.Pop();
		double num3 = num - 12.0;
		if (pour > 0.3)
		{
			PetArt.Line(d, PetArt.Wood, num2 - 15.0, 13.0 + 5.0 * pour, num3 + 1.0, 17.0, PetArt.SW * 1.1);
		}
		if (tip > 0.0)
		{
			d.PushTransform(new RotateTransform(-90.0 * tip, num3 - 6.0, 25.0));
			PetArt.Rect(d, PetArt.White, num3 - 6.0, 16.0, 11.0, 9.0, 2.5);
			d.Pop();
			if (puddle > 0.0)
			{
				PetArt.Oval(d, PetArt.Cyan, num3 - 16.0, 24.6, 4.0 + 10.0 * puddle, 1.8);
				PetArt.Drop(d, PetArt.Cyan, num - 27.0, 28.0 + 6.0 * puddle, 2.4);
			}
		}
		else
		{
			PetArt.Rect(d, PetArt.White, num3 - 6.0, 16.0, 11.0, 9.0, 2.5);
			PetArt.Ring(d, PetArt.White, num3 + 6.0, 20.5, 2.6, PetArt.SW);
			if (fill > 0.0)
			{
				PetArt.Rect(d, PetArt.Wood, num3 - 4.5, 16.8, 8.0, 2.2 * Math.Min(1.0, fill), 1.0);
			}
			double num4 = 14.0 - 3.0 * sip;
			PetArt.Curve(d, PetArt.White, PetArt.SW * 0.85, num3 - 1.0, num4, num3 - 4.0, num4 - 3.0, num3 - 1.0, num4 - 6.0);
			PetArt.Curve(d, PetArt.White, PetArt.SW * 0.85, num3 + 3.0, num4, num3, num4 - 3.0, num3 + 3.0, num4 - 6.0);
		}
		if (heart > 0.0)
		{
			double y = 7.0 - 3.0 * heart;
			d.PushOpacity(Math.Min(1.0, heart * 2.0));
			Heart(d, num3, y, 1.25);
			d.Pop();
		}
		d.Pop();
	}

	private static void Heart(DrawingContext d, double x, double y, double k)
	{
		PetArt.Dot(d, PetArt.Pink, x - 3.0 * k, y, 3.4 * k);
		PetArt.Dot(d, PetArt.Pink, x + 3.0 * k, y, 3.4 * k);
		PetArt.Poly(d, PetArt.Pink, null, x - 6.2 * k, y + 1.2 * k, x + 6.2 * k, y + 1.2 * k, x, y + 8.0 * k);
	}

	private static void PaperPlane(DrawingContext d, double x, double y, double angle, double k)
	{
		d.PushTransform(new RotateTransform(angle, x, y));
		PetArt.Poly(d, PetArt.White, null, x - 24.0 * k, y - 9.0 * k, x + 24.0 * k, y, x - 17.0 * k, y + 1.0 * k);
		PetArt.Poly(d, PetArt.White, PetArt.Ink, x - 17.0 * k, y + 1.0 * k, x + 24.0 * k, y, x - 21.0 * k, y + 7.0 * k);
		d.Pop();
	}

	private void PlaneRest(DrawingContext d, double a, bool settled, double u)
	{
		PaperPlane(d, a + 6.0, 30.0, -4.0, 1.1);
		PetArt.Rect(d, PetArt.Yellow, a + 30.0, 30.0, 18.0, 8.5, 1.5);
		PetArt.Rect(d, PetArt.Pink, a + 32.0, 27.0, 16.0, 4.0, 1.2);
		if (!(u < 0.0) && settled)
		{
			Point point = Hand();
			double num = 12.0 * (1.0 - 0.5 * u);
			if (u < 0.5)
			{
				PetArt.Rect(d, PetArt.White, point.X + 3.0, point.Y - 14.0, num, num, 1.0);
				return;
			}
			PetArt.Poly(d, PetArt.White, PetArt.Ink, point.X + 3.0, point.Y - 14.0, point.X + 3.0 + 14.0, point.Y - 8.0, point.X + 3.0, point.Y - 3.0);
		}
	}

	private void UmbrellaRain(DrawingContext d, double a, bool settled, double twirl, double t)
	{
		double num = a - 20.0;
		Cloud(d, num, 9.0, 1.1);
		for (int i = 0; i < 7; i++)
		{
			double num2 = num - 26.0 + (double)(i * 9);
			double y = 18.0 + (t * 24.0 + (double)i * 7.3) % 19.0;
			PetArt.Drop(d, PetArt.Cyan, num2, y, 2.6);
		}
		PetArt.Oval(d, PetArt.Cyan, a + 24.0, 37.6, 22.0, 2.1);
		if (settled)
		{
			HeldUmbrella(d, 1.0, twirl, t);
		}
	}

	private static void Cloud(DrawingContext d, double x, double y, double k)
	{
		PetArt.Oval(d, PetArt.White, x - 12.0 * k, y + 3.0 * k, 10.0 * k, 6.0 * k);
		PetArt.Oval(d, PetArt.White, x + 2.0 * k, y, 13.0 * k, 8.0 * k);
		PetArt.Oval(d, PetArt.White, x + 15.0 * k, y + 3.0 * k, 10.0 * k, 6.0 * k);
		PetArt.Rect(d, PetArt.White, x - 20.0 * k, y + 3.0 * k, 42.0 * k, 6.5 * k, 3.0 * k);
	}

	private void HeldUmbrella(DrawingContext d, double open, double twirl, double t)
	{
		Point point = Hand();
		Point point2 = new Point(point.X + 6.0, 3.5);
		PetArt.Line(d, PetArt.Wood, point.X, point.Y, point2.X, point2.Y + 8.0, PetArt.SW * 1.1);
		PetArt.Curve(d, PetArt.Wood, PetArt.SW * 1.1, point.X, point.Y, point.X - 1.0, point.Y + 4.0, point.X - 4.0, point.Y + 3.0);
		d.PushTransform(new RotateTransform(6.0 * Math.Sin(t * 6.0) * twirl, point2.X, point2.Y + 8.0));
		double num = 24.0 * Math.Abs(open);
		double num2 = ((open >= 0.0) ? (-9) : 20);
		StreamGeometry streamGeometry = new StreamGeometry();
		using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
		{
			streamGeometryContext.BeginFigure(new Point(point2.X - num, point2.Y + 9.0), isFilled: true, isClosed: true);
			streamGeometryContext.QuadraticBezierTo(new Point(point2.X, point2.Y + num2 - 2.0), new Point(point2.X + num, point2.Y + 9.0), isStroked: true, isSmoothJoin: true);
			int num3 = 4;
			for (int num4 = num3; num4 > 0; num4--)
			{
				double num5 = point2.X - num + 2.0 * num * (double)num4 / (double)num3;
				double num6 = point2.X - num + 2.0 * num * (double)(num4 - 1) / (double)num3;
				streamGeometryContext.QuadraticBezierTo(new Point((num5 + num6) / 2.0, point2.Y + 12.0), new Point(num6, point2.Y + 9.0), isStroked: true, isSmoothJoin: true);
			}
		}
		streamGeometry.Freeze();
		d.DrawGeometry(PetArt.Cyan, null, streamGeometry);
		for (int i = 1; i < 4; i++)
		{
			PetArt.Line(d, PetArt.White, point2.X, point2.Y + (double)((open >= 0.0) ? (-1) : 15), point2.X - num + 2.0 * num * (double)i / 4.0, point2.Y + 9.0, PetArt.SW * 0.6);
		}
		d.Pop();
	}

	private void ChalkBoard(DrawingContext d, double cx, string drawing, double progress, double wipe, double crumple)
	{
		PetArt.Rect(d, PetArt.Wood, cx - 34.0, 2.0, 68.0, 34.0, 2.5);
		PetArt.Rect(d, PetArt.Board, cx - 31.2, 4.6, 62.4, 28.8, 1.5);
		PetArt.Line(d, PetArt.Wood, cx - 24.0, 36.0, cx - 27.0, 38.5, PetArt.SW);
		PetArt.Line(d, PetArt.Wood, cx + 24.0, 36.0, cx + 27.0, 38.5, PetArt.SW);
		PetArt.Rect(d, PetArt.White, cx + 18.0, 33.0, 7.0, 2.6, 1.0);
		PetDrawing petDrawing = PetContent.Drawing(drawing);
		if (petDrawing == null || progress <= 0.0)
		{
			return;
		}
		double num = 1.0 - crumple;
		if (!(num <= 0.02))
		{
			Rect area = new Rect(cx - 14.0 * num, 5.8 + 14.0 * (1.0 - num), 28.0 * num, 26.0 * num);
			if (wipe > 0.0)
			{
				d.PushClip(new RectangleGeometry(new Rect(area.X + area.Width * wipe, 0.0, area.Width + 4.0, 40.0)));
			}
			DrawStrokes(d, petDrawing, area, progress, PetArt.White, PetArt.SW * 1.15);
			if (wipe > 0.0)
			{
				d.Pop();
			}
		}
	}

	private static void ChalkDust(DrawingContext d, double cx, double u)
	{
		for (int i = 0; i < 3; i++)
		{
			double num = (u + (double)i * 0.33) % 1.0;
			PetArt.Dot(d, PetArt.White, cx - 20.0 + (double)(i * 5), 30.0 + num * 6.0, 1.4 + num);
		}
	}

	private static void Ball(DrawingContext d, double x, double y, double r)
	{
		PetArt.Dot(d, PetArt.Cyan, x, y, r);
		PetArt.Curve(d, PetArt.White, PetArt.SW * 0.9, x - r * 0.9, y - r * 0.3, x, y + r * 0.6, x + r * 0.9, y - r * 0.3);
	}

	private static void BallPlay(DrawingContext d, double a, double u)
	{
		double num = ((u < 0.0) ? 31.8 : (31.8 - 18.0 * Math.Abs(Math.Sin(Math.PI * u * 2.0))));
		Ball(d, a - 22.0, num - 1.5, 8.2);
		if (u >= 0.0 && num > 30.0)
		{
			PetArt.Oval(d, PetArt.White, a - 22.0, 38.1, 4.0, 0.9);
		}
		PetArt.Polyline(d, PetArt.White, PetArt.SW * 1.1, a + 14.0, 38.5, a + 14.0, 18.0, a + 46.0, 18.0, a + 46.0, 38.5);
		for (int i = 1; i < 4; i++)
		{
			PetArt.Line(d, PetArt.White, a + 14.0 + (double)(i * 8), 18.0, a + 14.0 + (double)(i * 8), 38.5, PetArt.SW * 0.55);
		}
	}

	private static void Fish(DrawingContext d, double x, double y, double k, double flap)
	{
		PetArt.Oval(d, PetArt.Cyan, x, y, 8.0 * k, 4.6 * k);
		d.PushTransform(new RotateTransform(18.0 * flap, x - 7.0 * k, y));
		PetArt.Poly(d, PetArt.Cyan, null, x - 6.0 * k, y, x - 14.0 * k, y - 5.0 * k, x - 14.0 * k, y + 5.0 * k);
		d.Pop();
		PetArt.Dot(d, PetArt.Ink, x + 4.4 * k, y - 1.2 * k, 1.3);
	}

	private static void Boot(DrawingContext d, double x, double y)
	{
		PetArt.Poly(d, PetArt.Wood, null, x - 6.0, y - 12.0, x + 2.0, y - 12.0, x + 2.0, y - 3.0, x + 11.0, y - 1.0, x + 13.0, y + 2.0, x + 13.0, y + 4.0, x - 6.0, y + 4.0);
		PetArt.Rect(d, PetArt.White, x - 6.5, y + 3.0, 20.0, 2.6, 1.2);
		PetArt.Line(d, PetArt.White, x - 5.0, y - 8.0, x + 1.0, y - 8.0, PetArt.SW * 0.8);
	}

	private static void Bucket(DrawingContext d, double a, string outcome, double flap, double t)
	{
		double num = a - 14.0;
		if (outcome == "shoe")
		{
			Boot(d, num - 3.0, 14.0);
		}
		else if (outcome != "nothing")
		{
			d.PushTransform(new RotateTransform(-35.0, num, 14.0));
			Fish(d, num + 2.0, 13.0, 1.45, flap * Math.Sin(t * 14.0));
			d.Pop();
		}
		PetArt.Poly(d, PetArt.White, null, num - 12.0, 22.0, num + 12.0, 22.0, num + 9.0, 38.5, num - 9.0, 38.5);
		PetArt.Rect(d, PetArt.White, num - 13.5, 20.5, 27.0, 3.6, 1.5);
		PetArt.Line(d, PetArt.Cyan, num - 10.5, 29.0, num + 10.5, 29.0, PetArt.SW * 0.9);
		PetArt.Curve(d, PetArt.White, PetArt.SW, num - 12.0, 22.0, num, 9.0, num + 12.0, 22.0);
		if (outcome == "nothing")
		{
			PetArt.Oval(d, PetArt.Cyan, num, 21.6, 10.0, 1.6);
		}
		PetArt.Line(d, PetArt.Wood, a + 22.0, 38.5, a + 40.0, 5.0, PetArt.SW * 1.2);
		PetArt.Line(d, PetArt.White, a + 40.0, 5.0, a + 46.0, 24.0, PetArt.SW * 0.6);
		Bobber(d, a + 46.0, 27.0);
	}

	private static void Kite(DrawingContext d, double kx, double ky, double angle)
	{
		d.PushTransform(new RotateTransform(angle, kx, ky));
		PetArt.Poly(d, PetArt.Pink, null, kx, ky - 11.0, kx + 9.0, ky, kx, ky + 12.0, kx - 9.0, ky);
		PetArt.Line(d, PetArt.White, kx, ky - 11.0, kx, ky + 12.0, PetArt.SW * 0.75);
		PetArt.Line(d, PetArt.White, kx - 9.0, ky, kx + 9.0, ky, PetArt.SW * 0.75);
		PetArt.Polyline(d, PetArt.White, PetArt.SW * 0.8, kx, ky + 12.0, kx - 4.0, ky + 17.0, kx + 2.0, ky + 22.0, kx - 3.0, ky + 27.0);
		PetArt.Poly(d, PetArt.Yellow, null, kx - 4.0, ky + 15.5, kx - 7.5, ky + 13.5, kx - 7.5, ky + 18.5);
		PetArt.Poly(d, PetArt.Yellow, null, kx + 2.0, ky + 20.5, kx + 5.5, ky + 18.5, kx + 5.5, ky + 23.5);
		d.Pop();
	}

	private void KiteFly(DrawingContext d, double a, bool settled, double tug, double t)
	{
		double num = 4.0 * Math.Sin(t * 3.0) * tug;
		double num2 = a + 6.0 + num;
		double num3 = 12.0 + tug * Math.Sin(t * 5.0) * 2.0;
		Kite(d, num2, num3, num * 3.0);
		Point point = (settled ? Hand() : new Point(a - 94.0, 24.0));
		PetArt.Curve(d, PetArt.White, PetArt.SW * 0.6, point.X, point.Y, (point.X + num2) / 2.0, (point.Y + num3 + 12.0) / 2.0 + 5.0, num2, num3 + 12.0);
		Cloud(d, a - 40.0, 7.0, 0.7);
	}

	private static void Stumps(DrawingContext d, double sx, double bowled)
	{
		for (int i = 0; i < 3; i++)
		{
			d.PushTransform(new RotateTransform(bowled * (double)(i - 1) * 28.0, sx + (double)((i - 1) * 5), 38.5));
			PetArt.Line(d, PetArt.White, sx + (double)((i - 1) * 5), 38.5, sx + (double)((i - 1) * 5), 17.0, PetArt.SW * 1.15);
			d.Pop();
		}
		if (bowled < 0.1)
		{
			PetArt.Line(d, PetArt.White, sx - 6.0, 16.0, sx + 6.0, 16.0, PetArt.SW * 0.9);
			return;
		}
		PetArt.Line(d, PetArt.White, sx - 10.0, 9.0 - 8.0 * bowled, sx - 4.0, 10.0 - 8.0 * bowled, PetArt.SW * 0.9);
		PetArt.Line(d, PetArt.White, sx + 5.0, 8.0 - 9.0 * bowled, sx + 11.0, 10.0 - 9.0 * bowled, PetArt.SW * 0.9);
	}

	private void Bat(DrawingContext d, double swing)
	{
		Point point = Hand();
		double angle = 40.0 - 110.0 * swing;
		d.PushTransform(new RotateTransform(angle, point.X, point.Y));
		PetArt.Line(d, PetArt.Wood, point.X, point.Y, point.X + 8.0, point.Y, PetArt.SW * 0.9);
		PetArt.Rect(d, PetArt.Wood, point.X + 7.0, point.Y - 2.8, 20.0, 5.6, 2.2);
		d.Pop();
	}

	private void Cricket(DrawingContext d, double a, bool settled, double swing)
	{
		Stumps(d, a + 12.0, 0.0);
		PetArt.Dot(d, PetArt.Red, a - 24.0, 35.0, 3.6);
		PetArt.Line(d, PetArt.White, a - 26.5, 33.5, a - 21.5, 36.5, PetArt.SW * 0.6);
		if (settled)
		{
			Bat(d, swing);
		}
		else
		{
			PetArt.Rect(d, PetArt.Wood, a - 60.0, 22.0, 5.6, 20.0, 2.2);
		}
	}

	private static void Guitar(DrawingContext d, double gx, double strum, double t, double notes, double snap)
	{
		d.PushTransform(new ScaleTransform(1.3, 1.3, gx, 38.5));
		d.PushTransform(new RotateTransform(-28.0, gx, 28.0));
		PetArt.Line(d, PetArt.White, gx, 24.0, gx, 1.0, PetArt.SW * 1.6);
		PetArt.Rect(d, PetArt.White, gx - 3.5, -2.0, 7.0, 5.0, 1.5);
		PetArt.Oval(d, PetArt.Wood, gx, 30.0, 10.5, 8.5);
		PetArt.Oval(d, PetArt.Wood, gx, 20.5, 7.5, 6.3);
		PetArt.Dot(d, Brushes.Black, gx, 25.0, 3.2);
		double num = Math.Sin(t * 40.0) * strum * 0.8;
		if (snap <= 0.0)
		{
			for (int i = -1; i <= 1; i++)
			{
				PetArt.Line(d, PetArt.White, gx + (double)i * 1.6 + num, 35.0, gx + (double)i * 1.6, 2.0, PetArt.SW * 0.4);
			}
		}
		else
		{
			PetArt.Line(d, PetArt.White, gx - 1.6, 35.0, gx - 1.6, 2.0, PetArt.SW * 0.4);
			PetArt.Line(d, PetArt.White, gx + 1.6, 35.0, gx + 1.6, 2.0, PetArt.SW * 0.4);
			PetArt.Curve(d, PetArt.White, PetArt.SW * 0.55, gx, 2.0, gx + 8.0, 6.0, gx + 5.0, 12.0);
			PetArt.Curve(d, PetArt.White, PetArt.SW * 0.55, gx, 35.0, gx - 7.0, 31.0, gx - 4.0, 26.0);
		}
		PetArt.Rect(d, PetArt.Ink, gx - 4.0, 32.0, 8.0, 2.6, 1.0);
		d.Pop();
		d.Pop();
		if (notes > 0.0)
		{
			d.PushOpacity(Math.Min(1.0, notes * 3.0));
			double num2 = t * 6.0 % 10.0;
			PetArt.Note(d, PetArt.Yellow, gx + 40.0, 26.0 - num2, 10.0);
			PetArt.Note(d, PetArt.Yellow, gx + 60.0, 18.0 - num2 * 0.6, 8.0);
			PetArt.Note(d, PetArt.Yellow, gx + 78.0, 30.0 - num2 * 0.8, 9.0);
			d.Pop();
		}
		if (snap > 0.0)
		{
			PetArt.Star(d, PetArt.Yellow, gx + 10.0, 4.0, 4.0, 4, 0.35);
		}
	}

	private void Wand(DrawingContext d, out Point ring)
	{
		Point point = Hand();
		ring = new Point(point.X + 9.0, point.Y - 9.0);
		PetArt.Line(d, PetArt.White, point.X, point.Y, ring.X - 2.5, ring.Y + 2.5, PetArt.SW * 0.9);
		PetArt.Ring(d, PetArt.White, ring.X, ring.Y, 3.6, PetArt.SW * 0.9);
	}

	private static void Bubble(DrawingContext d, double x, double y, double r)
	{
		PetArt.Ring(d, PetArt.Cyan, x, y, r, PetArt.SW * 0.8);
		PetArt.Dot(d, PetArt.White, x - r * 0.42, y - r * 0.42, Math.Max(1.3, r * 0.16));
	}

	private void Bubbles(DrawingContext d, double a, bool settled, double t)
	{
		if (settled)
		{
			Wand(d, out var _);
		}
		double[] array = new double[5] { 4.0, 6.5, 3.5, 8.0, 5.0 };
		for (int i = 0; i < 5; i++)
		{
			double num = a - 48.0 + (double)(i * 22) + Math.Sin(t * 2.0 + (double)i) * 3.0;
			double num2 = 30.0 - (t * 6.0 + (double)(i * 9)) % 26.0;
			Bubble(d, num, (num2 < 5.0) ? (num2 + 26.0) : num2, array[i]);
		}
	}

	private void MagicHat(DrawingContext d, double hx, bool settled, double wave, double t)
	{
		PetArt.Oval(d, PetArt.White, hx - 4.0, 9.0, 2.4, 7.0);
		PetArt.Oval(d, PetArt.White, hx + 4.0, 8.0, 2.4, 7.5);
		PetArt.Oval(d, PetArt.Pink, hx - 4.0, 9.5, 1.0, 4.4);
		PetArt.Oval(d, PetArt.Pink, hx + 4.0, 8.5, 1.0, 4.8);
		PetArt.Poly(d, PetArt.White, null, hx - 11.0, 14.0, hx + 11.0, 14.0, hx + 12.5, 35.0, hx - 12.5, 35.0);
		PetArt.Rect(d, PetArt.Red, hx - 12.2, 27.5, 24.4, 4.6);
		PetArt.Oval(d, PetArt.White, hx, 35.6, 19.0, 3.2);
		double num = ((wave > 0.0) ? (0.6 + 0.4 * Math.Sin(t * 12.0)) : 1.0);
		PetArt.Star(d, PetArt.Yellow, hx + 12.0, 10.0, 3.4 * num, 4, 0.35);
		PetArt.Star(d, PetArt.Yellow, hx - 9.0, 8.0, 2.6, 4, 0.35);
		PetArt.Star(d, PetArt.Yellow, hx + 26.0, 20.0, 2.6 * num, 4, 0.35);
		if (settled)
		{
			Point point = Hand();
			double num2 = wave * 12.0 * Math.Sin(t * 7.0);
			Point point2 = new Point(point.X + 16.0, point.Y - 8.0 + num2 * 0.4);
			PetArt.Line(d, PetArt.White, point.X, point.Y, point2.X, point2.Y, PetArt.SW);
			PetArt.Star(d, PetArt.Yellow, point2.X + 1.0, point2.Y - 1.0, 3.6);
		}
	}

	private void Yoyo(DrawingContext d, double a, bool settled, double u)
	{
		Point point = (settled ? Hand() : new Point(a - 94.0, 27.0));
		double num = ((u < 0.0) ? 0.8 : Math.Abs(Math.Cos(Math.PI * u)));
		Point point2 = new Point(point.X + 2.0, point.Y + 3.0 + (34.0 - point.Y - 3.0) * num);
		PetArt.Line(d, PetArt.White, point.X, point.Y, point2.X, point2.Y, PetArt.SW * 0.6);
		PetArt.Dot(d, PetArt.Red, point2.X, point2.Y, 5.6);
		PetArt.Dot(d, PetArt.White, point2.X, point2.Y, 2.3);
		if (u >= 0.0)
		{
			PetArt.Line(d, PetArt.White, point2.X + 8.0, point2.Y - 4.0, point2.X + 8.0, point2.Y + 4.0, PetArt.SW * 0.7);
			PetArt.Line(d, PetArt.White, point2.X - 8.0, point2.Y - 4.0, point2.X - 8.0, point2.Y + 4.0, PetArt.SW * 0.7);
		}
		double num2 = a + 8.0;
		PetArt.Poly(d, PetArt.Yellow, null, num2 - 11.0, 9.0, num2 + 11.0, 9.0, num2 + 8.0, 21.0, num2 - 8.0, 21.0);
		PetArt.Ring(d, PetArt.Yellow, num2 - 11.0, 13.5, 4.0, PetArt.SW);
		PetArt.Ring(d, PetArt.Yellow, num2 + 11.0, 13.5, 4.0, PetArt.SW);
		PetArt.Rect(d, PetArt.Yellow, num2 - 2.5, 21.0, 5.0, 7.0);
		PetArt.Rect(d, PetArt.White, num2 - 10.0, 28.0, 20.0, 5.0, 1.5);
		PetArt.Rect(d, PetArt.White, num2 - 12.0, 33.0, 24.0, 5.5, 1.5);
		PetArt.Star(d, PetArt.Yellow, num2 + 28.0, 8.0, 3.4, 4, 0.35);
		PetArt.Star(d, PetArt.Yellow, num2 - 30.0, 14.0, 2.8, 4, 0.35);
		PetArt.Dot(d, PetArt.Red, num2 + 34.0, 31.0, 4.2);
		PetArt.Dot(d, PetArt.White, num2 + 34.0, 31.0, 1.8);
	}

	private void Bin(DrawingContext d, double a, bool settled, double u)
	{
		BinShape(d, a + 18.0);
		PetArt.Dot(d, PetArt.White, a - 22.0, 35.0, 3.5);
		PetArt.Dot(d, PetArt.White, a - 31.0, 35.4, 3.1);
		PetArt.Dot(d, PetArt.White, a - 12.0, 35.6, 2.9);
		if (u >= 0.0 && settled)
		{
			Point point = Hand();
			double num = point.X + 3.0 + (a + 18.0 - point.X - 3.0) * u;
			double y = point.Y - 4.0 + (18.0 - point.Y + 4.0) * u - 26.0 * Math.Sin(Math.PI * u);
			PetArt.Dot(d, PetArt.White, num, y, 3.2);
		}
	}

	private static void BinShape(DrawingContext d, double bx)
	{
		PetArt.Poly(d, PetArt.White, null, bx - 12.0, 18.0, bx + 12.0, 18.0, bx + 9.0, 38.5, bx - 9.0, 38.5);
		PetArt.Rect(d, PetArt.White, bx - 14.0, 15.6, 28.0, 3.6, 1.5);
		for (int i = -1; i <= 1; i++)
		{
			PetArt.Line(d, PetArt.Ink, bx + (double)i * 5.5, 22.0, bx + (double)i * 4.5, 35.0, PetArt.SW * 0.75);
		}
	}

	private static void Dominoes(DrawingContext d, double a, double fall, double place)
	{
		for (int i = 0; i < 9; i++)
		{
			double num = a - 46.0 + (double)i * 10.5;
			double num2 = Math.Max(0.0, Math.Min(1.0, fall * 9.0 - (double)i));
			double num3 = ((place >= 0.0 && i == 8) ? (-12.0 * (1.0 - place)) : 0.0);
			if (!(place >= 0.0) || i != 8 || !(place < 0.05))
			{
				d.PushTransform(new RotateTransform(num2 * 72.0, num + 5.5, 38.5));
				PetArt.Rect(d, PetArt.White, num, 19.5 + num3, 5.5, 19.0, 1.0);
				PetArt.Dot(d, PetArt.Ink, num + 2.75, 24.5 + num3, 1.35);
				PetArt.Dot(d, PetArt.Ink, num + 2.75, 32.0 + num3, 1.35);
				d.Pop();
			}
		}
	}

	private static void Pillow(DrawingContext d, double px)
	{
		PetArt.Rect(d, PetArt.White, px - 20.0, 31.5, 40.0, 8.0, 4.0);
		PetArt.Line(d, PetArt.Ink, px - 14.0, 35.4, px + 14.0, 35.4, PetArt.SW * 0.8);
	}

	private void DrawSurroundings(DrawingContext d)
	{
		double num = P(44.0);
		string festivalNow = FestivalNow;
		string weatherNow = WeatherNow;
		switch (festivalNow)
		{
		case "diwali":
		case "birthday":
		case "christmas":
			d.PushTransform(new ScaleTransform(1.25, 1.25, num, 38.5));
			DrawFestival(d, num, festivalNow);
			d.Pop();
			return;
		}
		DrawFestival(d, num, festivalNow);
		if (festivalNow != null || scene != null)
		{
			return;
		}
		switch (weatherNow)
		{
		case "rain":
		{
			Cloud(d, num + 1.0, 5.0, 0.55);
			for (int j = 0; j < 3; j++)
			{
				PetArt.Drop(d, PetArt.Cyan, num - 7.0 + (double)(j * 8), 16 + j * 5, 2.4);
			}
			PetArt.Oval(d, PetArt.Cyan, num, 37.6, 13.0, 2.0);
			break;
		}
		case "hot":
		{
			PetArt.Line(d, PetArt.White, num, 24.0, num, 36.5, PetArt.SW * 1.2);
			PetArt.Rect(d, PetArt.White, num - 8.0, 36.0, 16.0, 2.5, 1.2);
			PetArt.Ring(d, PetArt.White, num, 16.0, 8.0, PetArt.SW);
			for (int k = 0; k < 3; k++)
			{
				d.PushTransform(new RotateTransform(k * 120, num, 16.0));
				PetArt.Oval(d, PetArt.Cyan, num, 11.6, 2.6, 4.4);
				d.Pop();
			}
			PetArt.Dot(d, PetArt.White, num, 16.0, 1.8);
			break;
		}
		case "cold":
		{
			Point[] array = new Point[3]
			{
				new Point(num - 8.0, 9.0),
				new Point(num + 7.0, 17.0),
				new Point(num - 3.0, 28.0)
			};
			for (int i = 0; i < array.Length; i++)
			{
				Point point = array[i];
				PetArt.Star(d, PetArt.White, point.X, point.Y, 3.6, 6, 0.3);
			}
			break;
		}
		}
	}

	private void DrawFestival(DrawingContext d, double lx, string f)
	{
		switch (f)
		{
		case "diwali":
			PetArt.Poly(d, PetArt.Orange, null, lx - 11.0, 31.0, lx + 11.0, 31.0, lx + 7.0, 38.5, lx - 7.0, 38.5);
			PetArt.Oval(d, PetArt.Wood, lx, 31.0, 11.5, 2.4);
			PetArt.Drop(d, PetArt.Yellow, lx, 24.0, 4.4);
			PetArt.Dot(d, PetArt.Yellow, lx - 15.0, 37.5, 1.5);
			PetArt.Dot(d, PetArt.Orange, lx + 15.0, 37.5, 1.5);
			break;
		case "birthday":
			PetArt.Rect(d, PetArt.Pink, lx - 12.0, 27.0, 24.0, 11.5, 3.0);
			PetArt.Rect(d, PetArt.White, lx - 12.0, 27.0, 24.0, 4.5, 2.0);
			PetArt.Dot(d, PetArt.White, lx - 7.0, 31.5, 2.0);
			PetArt.Dot(d, PetArt.White, lx + 5.0, 31.8, 2.2);
			PetArt.Rect(d, PetArt.Cyan, lx - 1.6, 18.5, 3.2, 8.5, 1.0);
			PetArt.Drop(d, PetArt.Yellow, lx, 15.0, 3.6);
			break;
		case "christmas":
			PetArt.Poly(d, PetArt.Green, null, lx, 8.0, lx + 11.0, 22.0, lx - 11.0, 22.0);
			PetArt.Poly(d, PetArt.Green, null, lx, 15.0, lx + 14.0, 34.0, lx - 14.0, 34.0);
			PetArt.Rect(d, PetArt.Wood, lx - 3.0, 34.0, 6.0, 4.5);
			PetArt.Star(d, PetArt.Yellow, lx, 7.5, 4.0);
			PetArt.Dot(d, PetArt.Red, lx - 5.0, 26.0, 1.8);
			PetArt.Dot(d, PetArt.Yellow, lx + 6.0, 29.0, 1.8);
			PetArt.Dot(d, PetArt.Red, lx + 3.0, 18.0, 1.6);
			break;
		case "newyear":
		{
			for (int i = 0; i < 10; i++)
			{
				Brush b = ((i % 3 == 0) ? PetArt.Pink : ((i % 3 == 1) ? PetArt.Yellow : PetArt.Cyan));
				double num = P(22.0) + (double)i * P(26.0);
				d.PushTransform(new RotateTransform(i * 37, num, 4 + i * 7 % 9));
				PetArt.Rect(d, b, num - 1.5, 2 + i * 7 % 9, 3.0, 5.0, 0.6);
				d.Pop();
			}
			PetArt.Poly(d, PetArt.Yellow, null, lx - 6.0, 38.5, lx + 6.0, 38.5, lx + 10.0, 22.0);
			PetArt.Line(d, PetArt.Pink, lx + 10.0, 22.0, lx + 16.0, 14.0, PetArt.SW * 0.9);
			break;
		}
		case "holi":
			PetArt.Oval(d, PetArt.Pink, lx - 6.0, 36.5, 10.0, 2.6);
			PetArt.Oval(d, PetArt.Yellow, lx + 9.0, 37.2, 7.0, 2.0);
			PetArt.Oval(d, PetArt.Green, lx - 1.0, 33.6, 6.0, 1.8);
			PetArt.Dot(d, PetArt.Pink, lx - 8.0, 24.0, 3.4);
			PetArt.Dot(d, PetArt.Yellow, lx + 6.0, 18.0, 3.0);
			PetArt.Dot(d, PetArt.Green, lx - 2.0, 12.0, 2.6);
			break;
		}
	}

	private static void DoneBurst(DrawingContext d, double a, double b)
	{
		d.PushOpacity(Math.Min(1.0, b * 1.6));
		for (int i = 0; i < 8; i++)
		{
			double num = (double)i * Math.PI / 4.0;
			PetArt.Line(d, PetArt.Yellow, a + Math.Cos(num) * (6.0 + 6.0 * b), 9.0 + Math.Sin(num) * (4.0 + 4.0 * b), a + Math.Cos(num) * (10.0 + 8.0 * b), 9.0 + Math.Sin(num) * (6.0 + 5.0 * b), PetArt.SW * 0.9);
		}
		PetArt.Dot(d, PetArt.Green, a, 9.0, 5.5);
		PetArt.Polyline(d, PetArt.White, PetArt.SW, a - 2.6, 9.0, a - 0.6, 11.2, a + 3.0, 6.8);
		d.Pop();
	}

	private void DrawMicroFx(DrawingContext d)
	{
		PetLife l = L;
		string action = l.Action;
		if (action == null)
		{
			return;
		}
		double actionPhase = l.ActionPhase;
		double actionTime = l.ActionTime;
		if (actionPhase < 0.05)
		{
			return;
		}
		double num = P(x);
		double num2 = num + 24.0;
		double num3 = 10.0;
		d.PushOpacity(Math.Min(1.0, actionPhase * 2.0));
		switch (action)
		{
		case "time":
			PetArt.Dot(d, PetArt.White, num2, num3, 7.5);
			PetArt.Line(d, PetArt.Ink, num2, num3, num2, num3 - 5.0, PetArt.SW * 0.9);
			PetArt.Line(d, PetArt.Ink, num2, num3, num2 + 3.6, num3 + 1.4, PetArt.SW * 0.9);
			PetArt.Rect(d, PetArt.White, num2 - 2.0, num3 - 10.5, 4.0, 2.6, 0.8);
			break;
		case "battery":
			PetArt.Rect(d, null, num2 - 8.0, num3 - 4.5, 15.0, 9.0, 2.0, PetArt.White);
			PetArt.Rect(d, PetArt.White, num2 + 7.6, num3 - 2.0, 2.4, 4.0, 0.8);
			PetArt.Rect(d, PetArt.Green, num2 - 6.0, num3 - 2.5, 8.0 * actionPhase + 2.0, 5.0, 1.0);
			break;
		case "sneeze":
		{
			for (int m = 0; m < 5; m++)
			{
				double num6 = -0.6 + (double)m * 0.3;
				double num7 = 5.0 + 9.0 * actionPhase;
				PetArt.Dot(d, PetArt.White, num + 15.0 + Math.Cos(num6) * num7, 16.0 + Math.Sin(num6) * num7, 1.4 + 0.3 * (double)m % 2.0);
			}
			break;
		}
		case "hum":
		case "whistle":
			PetArt.Note(d, PetArt.Yellow, num2, num3 + 4.0 - 3.0 * actionPhase, 9.0);
			PetArt.Note(d, PetArt.Yellow, num2 + 9.0, num3 - 2.0 * actionPhase, 6.5);
			break;
		case "sigh":
			PetArt.Oval(d, PetArt.White, num2, num3 + 4.0, 4.4, 3.0);
			PetArt.Oval(d, PetArt.White, num2 + 5.0, num3 + 1.0, 5.0, 3.6);
			PetArt.Oval(d, PetArt.White, num2 + 9.0, num3 + 4.0, 3.6, 2.6);
			break;
		case "yawn":
			PetArt.Ring(d, PetArt.White, num2, num3 + 2.0, 3.6 + 1.6 * actionPhase, PetArt.SW);
			PetArt.Zee(d, num2 + 6.0, num3 - 8.0, 4.6);
			break;
		case "rub-eyes":
			PetArt.Zee(d, num2, num3 - 4.0, 5.0);
			break;
		case "clap":
		{
			for (int num10 = 0; num10 < 6; num10++)
			{
				double num11 = (double)num10 * Math.PI / 3.0;
				PetArt.Line(d, PetArt.Yellow, num2 + Math.Cos(num11) * 3.0, num3 + Math.Sin(num11) * 3.0, num2 + Math.Cos(num11) * (5.0 + 3.0 * actionPhase), num3 + Math.Sin(num11) * (5.0 + 3.0 * actionPhase), PetArt.SW * 0.85);
			}
			break;
		}
		case "cheer":
		{
			for (int n = 0; n < 6; n++)
			{
				Brush b = ((n % 3 == 0) ? PetArt.Pink : ((n % 3 == 1) ? PetArt.Yellow : PetArt.Cyan));
				double num8 = num - 14.0 + (double)(n * 6);
				double num9 = 6.0 - 4.0 * actionPhase + (double)(n % 2 * 4);
				PetArt.Rect(d, b, num8 - 1.3, num9 - 2.0, 2.8, 4.2, 0.6);
			}
			break;
		}
		case "thumbs-up":
		case "salute":
			PetArt.Star(d, PetArt.Yellow, num2, num3, 5.0 + actionPhase);
			break;
		case "wipe-brow":
			PetArt.Drop(d, PetArt.Cyan, num - 17.0, 9.0 + 4.0 * actionPhase, 3.0);
			break;
		case "hiccup":
			PetArt.Ring(d, PetArt.White, num2, num3 + 4.0 - 5.0 * actionPhase, 2.6, PetArt.SW * 0.75);
			PetArt.Ring(d, PetArt.White, num2 + 5.0, num3 - 2.0 - 4.0 * actionPhase, 1.8, PetArt.SW * 0.7);
			break;
		case "dust-off":
			PetArt.Oval(d, PetArt.White, num - 15.0, 35.5, 3.4 * actionPhase + 1.0, 2.2);
			PetArt.Oval(d, PetArt.White, num + 16.0, 35.0, 3.0 * actionPhase + 1.0, 2.0);
			break;
		case "polish":
			PetArt.Star(d, PetArt.White, num + 6.0, 6.0, 4.6 + 1.4 * Math.Sin(actionTime * 12.0), 4, 0.28);
			PetArt.Star(d, PetArt.White, num + 16.0, 13.0, 2.6, 4, 0.3);
			break;
		case "scratch":
		case "shrug":
		case "look-around":
		case "peek":
			Question(d, num2, num3, actionPhase);
			break;
		case "stretch":
			PetArt.Curve(d, PetArt.White, PetArt.SW * 0.8, num - 20.0, 14.0, num - 24.0, 6.0, num - 18.0, 0.0);
			PetArt.Curve(d, PetArt.White, PetArt.SW * 0.8, num + 20.0, 14.0, num + 24.0, 6.0, num + 18.0, 0.0);
			break;
		case "hop":
			PetArt.Line(d, PetArt.White, num - 9.0, 38.0, num - 4.0, 38.0, PetArt.SW * 0.8);
			PetArt.Line(d, PetArt.White, num + 4.0, 38.0, num + 9.0, 38.0, PetArt.SW * 0.8);
			PetArt.Line(d, PetArt.White, num - 21.0, 24.0, num - 21.0, 32.0, PetArt.SW * 0.8);
			PetArt.Line(d, PetArt.White, num + 21.0, 24.0, num + 21.0, 32.0, PetArt.SW * 0.8);
			break;
		case "tap":
		{
			for (int k = 0; k < 3; k++)
			{
				double num5 = -Math.PI / 5.0 - (double)k * Math.PI * 0.3;
				PetArt.Line(d, PetArt.White, num + 10.0 + Math.Cos(num5) * 4.0, 36.0 + Math.Sin(num5) * 4.0, num + 10.0 + Math.Cos(num5) * 7.0, 36.0 + Math.Sin(num5) * 7.0, PetArt.SW * 0.75);
			}
			break;
		}
		case "nod":
			PetArt.Curve(d, PetArt.White, PetArt.SW * 0.8, num - 12.0, 2.5, num, -1.0, num + 12.0, 2.5);
			PetArt.Curve(d, PetArt.White, PetArt.SW * 0.8, num + 19.0, 6.0, num + 22.0, 10.0, num + 19.0, 14.0);
			break;
		case "kick":
			PetArt.Curve(d, PetArt.White, PetArt.SW * 0.8, num + 12.0, 30.0, num + 20.0, 32.0, num + 18.0, 38.5);
			PetArt.Oval(d, PetArt.White, num + 22.0, 37.5, 3.0, 1.6);
			break;
		case "wiggle":
		{
			int[] array = new int[2] { -1, 1 };
			foreach (int num4 in array)
			{
				PetArt.Curve(d, PetArt.White, PetArt.SW * 0.8, num + (double)(num4 * 19), 12.0, num + (double)(num4 * 23), 19.0, num + (double)(num4 * 19), 26.0);
				PetArt.Curve(d, PetArt.White, PetArt.SW * 0.8, num + (double)(num4 * 24), 14.0, num + (double)(num4 * 27), 19.0, num + (double)(num4 * 24), 24.0);
			}
			break;
		}
		case "tilt":
		{
			for (int i = 0; i < 3; i++)
			{
				PetArt.Dot(d, PetArt.White, num2 - 4.0 + (double)(i * 5), num3 + 2.0 - (double)i * 2.5, 1.8);
			}
			break;
		}
		case "double-blink":
			PetArt.Line(d, PetArt.White, num - 13.0, 4.0, num - 10.0, 6.0, PetArt.SW * 0.75);
			PetArt.Line(d, PetArt.White, num + 13.0, 4.0, num + 10.0, 6.0, PetArt.SW * 0.75);
			break;
		case "look-up":
			PetArt.Star(d, PetArt.Yellow, num2 - 6.0, 3.5, 3.4);
			break;
		case "look-left":
			PetArt.Line(d, PetArt.White, num - 19.0, 9.0, num - 25.0, 9.0, PetArt.SW * 0.8);
			PetArt.Line(d, PetArt.White, num - 19.0, 14.0, num - 27.0, 14.0, PetArt.SW * 0.8);
			break;
		case "look-right":
			PetArt.Line(d, PetArt.White, num + 19.0, 9.0, num + 25.0, 9.0, PetArt.SW * 0.8);
			PetArt.Line(d, PetArt.White, num + 19.0, 14.0, num + 27.0, 14.0, PetArt.SW * 0.8);
			break;
		case "wave-small":
			PetArt.Curve(d, PetArt.White, PetArt.SW * 0.8, num + 22.0, 2.0, num + 26.0, 6.0, num + 24.0, 11.0);
			PetArt.Curve(d, PetArt.White, PetArt.SW * 0.8, num + 27.0, 0.0, num + 31.0, 5.0, num + 29.0, 11.0);
			break;
		}
		d.Pop();
	}

	private static void Question(DrawingContext d, double x, double y, double m)
	{
		PetArt.Curve(d, PetArt.White, PetArt.SW * 1.1, x - 3.4, y - 3.4, x, y - 9.5, x + 3.4, y - 3.4);
		PetArt.Curve(d, PetArt.White, PetArt.SW * 1.1, x + 3.4, y - 3.4, x + 3.4, y - 0.6, x, y + 1.0);
		PetArt.Line(d, PetArt.White, x, y + 1.0, x, y + 2.4, PetArt.SW * 1.1);
		PetArt.Dot(d, PetArt.White, x, y + 6.0, 1.6);
	}

	private void DrawScene(DrawingContext d, string name)
	{
		double num = elapsed;
		switch (name)
		{
		case "tea":
		{
			double num7 = P(Value("p.x", 340.0));
			TeaTable(d, num7 + 26.0, 0.0, num, Value("fill", 0.0), Value("pour", 0.0), Value("tip", 0.0), Value("puddle", 0.0), Value("heart", 0.0));
			break;
		}
		case "plane":
		{
			double num29 = Value("fold", 0.0);
			if (num < 4.2)
			{
				Point point4 = Hand();
				double num30 = 12.0;
				if (num29 < 1.0)
				{
					PetArt.Rect(d, PetArt.White, point4.X + 3.0, point4.Y - 14.0, num30 - 3.0 * num29, num30, 1.0);
				}
				else if (num29 < 2.0)
				{
					PetArt.Poly(d, PetArt.White, PetArt.Ink, point4.X + 3.0, point4.Y - 14.0, point4.X + 16.0, point4.Y - 8.0, point4.X + 3.0, point4.Y - 2.0);
				}
				else
				{
					PaperPlane(d, point4.X + 12.0, point4.Y - 8.0, -8.0, 0.45);
				}
			}
			else
			{
				PaperPlane(d, P(Value("pl.x", 96.0)), Value("pl.y", 20.0), Value("pl.a", 0.0) + 8.0 * Value("wiggle", 0.0), 1.0);
			}
			break;
		}
		case "rain":
		{
			double num22 = P(Value("cloud.x", 340.0));
			double num23 = Value("rain", 0.0);
			Cloud(d, num22, 6.0, 1.2);
			if (num23 > 0.0)
			{
				d.PushOpacity(num23);
				for (int n = 0; n < 8; n++)
				{
					double num24 = num22 - 26.0 + (double)n * 7.6;
					double y = 17.0 + (num * 26.0 + (double)n * 6.3) % 20.0;
					PetArt.Drop(d, PetArt.Cyan, num24, y, 2.6);
				}
				d.Pop();
			}
			double num25 = Value("umb", 0.0);
			if (Math.Abs(num25) > 0.02)
			{
				HeldUmbrella(d, num25, 0.0, num);
			}
			double num26 = Value("wet", 0.0);
			if (num26 > 0.0)
			{
				d.PushOpacity(num26);
				double num27 = P(x);
				for (int num28 = 0; num28 < 4; num28++)
				{
					PetArt.Drop(d, PetArt.Cyan, num27 - 12.0 + (double)(num28 * 8), 8.0 + (num * 20.0 + (double)(num28 * 5)) % 22.0, 2.4);
				}
				d.Pop();
			}
			break;
		}
		case "chalk":
		{
			double cx = P(Value("b.x", 340.0));
			ChalkBoard(d, cx, DrawingNow ?? "smiley", Value("writing", 0.0), Value("wipe", 0.0), Value("crumple", 0.0));
			Point point5 = Hand();
			PetArt.Line(d, PetArt.White, point5.X, point5.Y, point5.X + 3.0, point5.Y - 3.0, PetArt.SW * 1.2);
			double num41 = Value("wad.x", -60.0);
			if (Value("crumple", 0.0) > 0.95 && num41 < 335.0)
			{
				PetArt.Dot(d, PetArt.White, P(num41), Value("wad.y", 18.0), 3.8);
			}
			break;
		}
		case "ball":
			Ball(d, P(Value("ball.x", 340.0)), Value("ball.y", 32.0) - 1.5, 8.2);
			if (Value("stars", 0.0) > 0.0)
			{
				double num42 = P(x);
				for (int num43 = 0; num43 < 3; num43++)
				{
					double num44 = num * 6.0 + (double)num43 * 2.1;
					PetArt.Star(d, PetArt.Yellow, num42 + Math.Cos(num44) * 13.0, 5.0 + Math.Sin(num44) * 2.6, 2.8);
				}
			}
			break;
		case "catch":
		{
			double num45 = P(Value("w.x", 340.0));
			bool flag = Math.Abs(Value("w.x", 0.0) - 184.0) < 1.0;
			double num46 = num45 - 62.0;
			double num47 = num45 + 52.0;
			PetArt.Rect(d, PetArt.Cyan, num46, 33.0, num47 - num46, 6.5, 2.5);
			for (double num48 = num46 + 6.0; num48 < num47 - 8.0; num48 += 16.0)
			{
				PetArt.Line(d, PetArt.White, num48, 34.7, num48 + 6.0, 34.7, PetArt.SW * 0.8);
			}
			Point point6 = Hand();
			Point point7 = new Point(point6.X + 44.0, point6.Y - 26.0 + Math.Max(0.0, Math.Sin(Value("armR", 60.0) * Math.PI / 180.0) - 0.8) * -10.0);
			point7 = new Point(Math.Min(point7.X, P(150.0)), Math.Max(2.0, point7.Y));
			PetArt.Line(d, PetArt.Wood, point6.X, point6.Y, point7.X, point7.Y, PetArt.SW * 1.3);
			double num49 = Value("line", 0.0);
			double num50 = Value("hook", 0.0);
			if (!(num49 > 0.0) || !flag)
			{
				break;
			}
			double num51 = num45 + 6.0;
			double num52 = 31.5 + 3.0 * Value("bob", 0.0);
			Point point8 = new Point(num51 + (point7.X + 4.0 - num51) * num50, num52 + (point7.Y + 14.0 - num52) * num50);
			PetArt.Line(d, PetArt.White, point7.X, point7.Y, point8.X, point8.Y - 3.6, PetArt.SW * 0.7);
			if (num50 < 0.05)
			{
				Bobber(d, point8.X, point8.Y);
			}
			else
			{
				string text = OutcomeNow ?? "fish";
				if (text == "fish")
				{
					Fish(d, point8.X, point8.Y + 3.0, 1.4, Math.Sin(num * 16.0));
				}
				else if (text == "shoe")
				{
					Boot(d, point8.X, point8.Y + 4.0);
				}
				else
				{
					PetArt.Curve(d, PetArt.White, PetArt.SW * 0.9, point8.X, point8.Y - 3.6, point8.X, point8.Y + 3.0, point8.X - 4.0, point8.Y + 1.0);
					if (num50 > 0.9)
					{
						Question(d, P(x) + 24.0, 10.0, 1.0);
					}
				}
			}
			if (Value("bob", 0.0) > 0.5)
			{
				d.DrawEllipse(null, PetArt.Pen(PetArt.White, PetArt.SW * 0.7), new Point(num51, 35.2), 8.0, 1.9);
			}
			break;
		}
		case "kite":
		{
			double num8 = P(Value("k.x", 96.0));
			double num9 = Value("k.y", 22.0);
			Kite(d, num8, num9, Value("spin", 0.0) * 360.0 + Value("sway", 0.0) * 12.0);
			Point point = Hand();
			if (Value("tangle", 0.0) > 0.0)
			{
				PetArt.Curve(d, PetArt.White, PetArt.SW * 0.6, point.X, point.Y, (point.X + num8) / 2.0, 30.0, num8 - 10.0, 30.0);
				for (int k = 0; k < 3; k++)
				{
					PetArt.Ring(d, PetArt.White, num8 - 14.0 + (double)(k * 5), 31 - k, 3.4, PetArt.SW * 0.6);
				}
			}
			else
			{
				PetArt.Curve(d, PetArt.White, PetArt.SW * 0.6, point.X, point.Y, (point.X + num8) / 2.0, (point.Y + num9 + 12.0) / 2.0 + 5.0, num8, num9 + 12.0);
			}
			break;
		}
		case "cricket":
		{
			Stumps(d, P(Value("st.x", -40.0)), Value("bowled", 0.0));
			Bat(d, Value("swing", 0.0));
			double num10 = P(Value("ball.x", 340.0));
			double num11 = Value("ball.y", 30.0);
			PetArt.Dot(d, PetArt.Red, num10, num11, 3.6);
			PetArt.Line(d, PetArt.White, num10 - 2.5, num11 - 1.5, num10 + 2.5, num11 + 1.5, PetArt.SW * 0.6);
			break;
		}
		case "guitar":
			Guitar(d, P(Value("g.x", 340.0)), (Value("notes", 0.0) > 0.0 || Value("snap", 0.0) > 0.0) ? 1 : 0, num, Value("notes", 0.0), Value("snap", 0.0));
			break;
		case "bubbles":
		{
			Wand(d, out var _);
			double num16 = Value("bub.r", 0.0);
			double num17 = P(Value("bub.x", 104.0));
			double num18 = Value("bub.y", 20.0);
			if (num16 > 0.3)
			{
				Bubble(d, num17, num18, num16);
			}
			double num19 = Value("pop", 0.0);
			if (num19 > 0.0)
			{
				for (int m = 0; m < 8; m++)
				{
					double num20 = (double)m * Math.PI / 4.0;
					double num21 = 9.0 + 6.0 * (num - 7.0);
					PetArt.Dot(d, PetArt.Cyan, num17 + Math.Cos(num20) * num21, num18 + Math.Sin(num20) * num21, 1.6);
				}
			}
			break;
		}
		case "magic":
		{
			double num36 = P(Value("h.x", 340.0));
			MagicHat(d, num36, settled: true, 1.0, num);
			double num37 = Value("star", 0.0);
			if (num37 > 0.0)
			{
				PetArt.Star(d, PetArt.Yellow, num36, 28.0 - 22.0 * num37, 4.0 + 4.0 * num37);
				for (int num38 = 0; num38 < 4; num38++)
				{
					double num39 = num * 4.0 + (double)num38 * 1.57;
					PetArt.Star(d, PetArt.White, num36 + Math.Cos(num39) * 14.0 * num37, 10.0 + Math.Sin(num39) * 6.0 * num37, 2.2, 4, 0.35);
				}
			}
			double num40 = Value("puff", 0.0);
			if (num40 > 0.0)
			{
				d.PushOpacity(Math.Min(1.0, num40));
				PetArt.Oval(d, PetArt.White, num36 - 6.0, 12.0 - 4.0 * num40, 6.0, 4.4);
				PetArt.Oval(d, PetArt.White, num36 + 5.0, 9.0 - 5.0 * num40, 7.0, 5.0);
				PetArt.Oval(d, PetArt.White, num36, 5.0 - 5.0 * num40, 5.0, 3.6);
				d.Pop();
			}
			break;
		}
		case "yoyo":
		{
			Point point2 = Hand();
			double num12 = Value("yo", 0.0);
			if (num12 < 0.05)
			{
				break;
			}
			double num13 = Value("loop", 0.0);
			double num14 = Value("yy", 0.0);
			Point point3;
			if (num13 > 0.0 && num13 < 1.0)
			{
				double num15 = Math.PI / 2.0 + num13 * Math.PI * 2.0;
				point3 = new Point(point2.X + 14.0 + Math.Cos(num15) * 12.0, point2.Y - 6.0 + Math.Sin(num15) * 12.0);
				PetArt.Star(d, PetArt.Yellow, point2.X + 30.0, 8.0, 3.4, 4, 0.35);
			}
			else
			{
				point3 = new Point(point2.X + 2.0, point2.Y + 3.0 + 12.0 * num14);
			}
			PetArt.Line(d, PetArt.White, point2.X, point2.Y, point3.X, point3.Y, PetArt.SW * 0.6);
			if (Value("tangle", 0.0) > 0.0)
			{
				for (int l = 0; l < 3; l++)
				{
					PetArt.Ring(d, PetArt.White, (point2.X + point3.X) / 2.0 + (double)(l * 2) - 2.0, (point2.Y + point3.Y) / 2.0 + (double)l - 1.0, 2.6, PetArt.SW * 0.6);
				}
			}
			PetArt.Dot(d, PetArt.Red, point3.X, point3.Y, 5.2);
			PetArt.Dot(d, PetArt.White, point3.X, point3.Y, 2.2);
			break;
		}
		case "hoop":
		{
			double num31 = P(Value("bin.x", 340.0));
			BinShape(d, num31);
			double num32 = P(Value("wad.x", 96.0));
			double y2 = Value("wad.y", 24.0);
			if (!(Value("burst", 0.0) > 0.0) || !(num > 6.1))
			{
				PetArt.Dot(d, PetArt.White, num32, y2, 3.4);
			}
			double num33 = Value("burst", 0.0);
			if (num33 > 0.0)
			{
				for (int num34 = 0; num34 < 8; num34++)
				{
					double num35 = (double)num34 * Math.PI / 4.0;
					PetArt.Line(d, PetArt.Yellow, num31 + Math.Cos(num35) * 14.0, 12.0 + Math.Sin(num35) * 7.0, num31 + Math.Cos(num35) * 19.0, 12.0 + Math.Sin(num35) * 9.5, PetArt.SW * 0.9);
				}
			}
			break;
		}
		case "dominoes":
			Dominoes(d, P(Value("d.x", 340.0)), Value("fall", 0.0), -1.0);
			break;
		case "rocket-launch":
		{
			double num2 = P(Value("rocket.x", 340.0));
			double offsetY = Value("rocket.y", 0.0);
			double num3 = Value("smoke", 0.0);
			d.PushTransform(new TranslateTransform(0.0, offsetY));
			Rocket(d, num2, 38.5, 7, Value("flame", 0.0), 0.0);
			d.Pop();
			if (num3 > 0.0)
			{
				d.PushOpacity(num3);
				for (int i = 0; i < 4; i++)
				{
					double num4 = 4.0 + num3 * 4.0 + (double)i;
					PetArt.Oval(d, PetArt.White, num2 + ((double)i - 1.5) * 10.0, 36.5, num4, num4 * 0.6);
				}
				d.Pop();
			}
			double num5 = Value("count", 0.0);
			if (num5 > 0.0 && num5 < 3.0)
			{
				int num6 = 3 - (int)num5;
				for (int j = 0; j < num6; j++)
				{
					PetArt.Dot(d, PetArt.Yellow, num2 + 32.0, 10 + j * 9, 3.6);
				}
			}
			break;
		}
		case "first-meet":
		case "intro-kick":
			BoxBack(d);
			break;
		}
	}

	private void BoxBack(DrawingContext d)
	{
		double boxX = BoxX;
		double num = ((introBox && (scene == null || !scene.Tracks.ContainsKey("box.lid"))) ? 1.0 : Value("box.lid", 0.0));
		double angle = ((scene != null) ? Value("box.rot", 0.0) : 0.0);
		if (!(boxX > P(340.0) - 5.0))
		{
			bool flag = scene != null && scene.Name.StartsWith("moment-", StringComparison.Ordinal);
			if (flag)
			{
				d.PushOpacity(0.18);
			}
			d.PushTransform(new RotateTransform(angle, boxX, 38.5));
			PetArt.Rect(d, PetArt.Wood, boxX - 22.0, 12.0, 44.0, 26.5, 2.0);
			d.PushTransform(new TranslateTransform((0.0 - num) * 6.0, (0.0 - num) * 5.0));
			d.PushTransform(new RotateTransform((0.0 - num) * 32.0, boxX - 22.0, 12.0));
			PetArt.Rect(d, PetArt.Orange, boxX - 23.0, 8.5, 46.0, 5.0, 1.8);
			d.Pop();
			d.Pop();
			d.Pop();
			if (flag)
			{
				d.Pop();
			}
		}
	}

	private void DrawBoxFront(DrawingContext d)
	{
		double boxX = BoxX;
		double angle = ((scene != null) ? Value("box.rot", 0.0) : 0.0);
		if (!(boxX > P(340.0) - 5.0))
		{
			PetArt.SW = Math.Max(1.9, Math.Min(3.4, 1.8 / Math.Max(0.3, unit)));
			bool flag = scene != null && scene.Name.StartsWith("moment-", StringComparison.Ordinal);
			if (flag)
			{
				d.PushOpacity(0.18);
			}
			d.PushTransform(new RotateTransform(angle, boxX, 38.5));
			PetArt.Rect(d, PetArt.Wood, boxX - 22.0, 19.0, 44.0, 19.5, 2.0);
			PetArt.Line(d, PetArt.White, boxX - 22.0, 24.0, boxX + 22.0, 24.0, PetArt.SW * 0.9);
			PetArt.Poly(d, PetArt.Red, null, boxX - 4.0, 26.0, boxX + 4.0, 26.0, boxX, 33.0);
			PetArt.Dot(d, PetArt.Red, boxX - 2.0, 25.5, 2.2);
			PetArt.Dot(d, PetArt.Red, boxX + 2.0, 25.5, 2.2);
			d.Pop();
			if (flag)
			{
				d.Pop();
			}
		}
	}

	internal static List<PetTile> ContactTiles()
	{
		List<PetTile> list = new List<PetTile>();
		PetActivity[] activities = PetContent.Activities;
		foreach (PetActivity petActivity in activities)
		{
			list.Add(new PetTile
			{
				Kind = "activity",
				Name = petActivity.Name,
				Action = signature[petActivity.Name],
				U = (signatureU.TryGetValue(petActivity.Name, out var value) ? value : 0.5),
				Label = "activity " + petActivity.Name
			});
		}
		PetBigScene[] bigScenes = PetContent.BigScenes;
		foreach (PetBigScene petBigScene in bigScenes)
		{
			string action = signature[petBigScene.Aftermath];
			list.Add(new PetTile
			{
				Kind = "after",
				Name = petBigScene.Aftermath,
				Action = action,
				U = (signatureU.TryGetValue(petBigScene.Aftermath, out var value2) ? value2 : 0.5),
				Outcome = petBigScene.Outcomes[0],
				Drawing = "flower",
				Label = "after " + petBigScene.Name + " (" + petBigScene.Aftermath + ")"
			});
			if (!(petBigScene.Aftermath == "bucket"))
			{
				continue;
			}
			foreach (string item in petBigScene.Outcomes.Skip(1))
			{
				list.Add(new PetTile
				{
					Kind = "after",
					Name = "bucket",
					Action = "flap",
					Outcome = item,
					Label = "after catch (bucket, " + item + ")"
				});
			}
		}
		string[] micros = PetContent.Micros;
		foreach (string text in micros)
		{
			list.Add(new PetTile
			{
				Kind = "micro",
				Name = text,
				Action = text,
				U = ((text == "double-blink") ? 0.06 : 0.5),
				Label = "micro " + text
			});
		}
		PetBigScene[] bigScenes2 = PetContent.BigScenes;
		foreach (PetBigScene petBigScene2 in bigScenes2)
		{
			string[] outcomes = petBigScene2.Outcomes;
			foreach (string text2 in outcomes)
			{
				GardenScene s = GardenScenes.Big(petBigScene2.Name, text2, 78.0);
				list.Add(new PetTile
				{
					Kind = "big",
					Name = petBigScene2.Name,
					Outcome = text2,
					Drawing = "star",
					Time = GardenScenes.OutcomeTime(s) + (outcomeOffset.TryGetValue(petBigScene2.Name + "/" + text2, out var value3) ? value3 : 0.5),
					Label = "scene " + petBigScene2.Name + " / " + text2
				});
			}
		}
		for (int n = 0; n <= 7; n++)
		{
			list.Add(new PetTile
			{
				Kind = "activity",
				Name = "tinkering",
				Action = "hammer",
				U = 0.327,
				Rocket = n,
				Label = "story rocket day " + n
			});
		}
		list.Add(new PetTile
		{
			Kind = "launch",
			Time = 5.2,
			Label = "story rocket countdown"
		});
		list.Add(new PetTile
		{
			Kind = "launch",
			Time = 9.4,
			Label = "story rocket lift-off"
		});
		int[] array = new int[5] { 0, 3, 10, 20, 30 };
		string[] array2 = new string[5] { "seed", "sprout", "leaves", "bud", "flower" };
		for (int num = 0; num < 5; num++)
		{
			list.Add(new PetTile
			{
				Kind = "activity",
				Name = "gardening",
				Action = "water",
				Plant = array[num],
				Label = "story plant " + array2[num]
			});
		}
		int[] array3 = new int[5] { 0, 5, 10, 15, 20 };
		foreach (int num3 in array3)
		{
			list.Add(new PetTile
			{
				Kind = "activity",
				Name = "stargazing",
				Action = "peer",
				U = 0.65,
				Stars = num3,
				Label = "story star jar " + num3
			});
		}
		object[][] array4 = new object[8][]
		{
			new object[2] { 0.7, "box slides in" },
			new object[2] { 1.8, "box wobbles" },
			new object[2] { 3.0, "lid pops" },
			new object[2] { 3.65, "eyes peek" },
			new object[2] { 4.45, "jumps out" },
			new object[2] { 4.85, "lands (squash)" },
			new object[2] { 5.3, "looks around" },
			new object[2] { 6.0, "waves" }
		};
		foreach (object[] array5 in array4)
		{
			list.Add(new PetTile
			{
				Kind = "intro",
				Time = (double)array5[0],
				Label = "intro " + array5[1]
			});
		}
		list.Add(new PetTile
		{
			Kind = "intro-hello",
			Time = 4.0,
			Label = "intro Hi, I'm Ayn!"
		});
		list.Add(new PetTile
		{
			Kind = "intro-kick",
			Time = 1.5,
			Label = "intro kicks the box away"
		});
		string[][] array6 = new string[4][]
		{
			new string[3] { "screenshot", "camera", "Screenshot saved" },
			new string[3] { "download", "box", "report.pdf" },
			new string[3] { "shelf", "box", "Saved to Shelf" },
			new string[3] { "say-ctx/music", "wave", "Nice song!" }
		};
		foreach (string[] array7 in array6)
		{
			list.Add(new PetTile
			{
				Kind = "message",
				Name = array7[0],
				Action = array7[1],
				Outcome = array7[2],
				Label = "message " + array7[2]
			});
		}
		micros = new string[5] { "diwali", "holi", "christmas", "newyear", "birthday" };
		foreach (string text3 in micros)
		{
			list.Add(new PetTile
			{
				Kind = "festival",
				Festival = text3,
				Label = "festival " + text3
			});
		}
		micros = new string[3] { "rain", "hot", "cold" };
		foreach (string text4 in micros)
		{
			list.Add(new PetTile
			{
				Kind = "festival",
				Weather = text4,
				Label = "weather " + text4
			});
		}
		PetDrawing[] drawings = PetContent.Drawings;
		foreach (PetDrawing petDrawing in drawings)
		{
			list.Add(new PetTile
			{
				Kind = "drawing",
				Drawing = petDrawing.Name,
				Label = "drawing " + petDrawing.Name
			});
		}
		return list;
	}

	private static PetActivity Aftermath(string prop)
	{
		double right = prop switch
		{
			"bat" => 60, 
			"hat" => 80, 
			"yoyo" => 85, 
			"wand" => 90, 
			"umbrella" => 100, 
			"kite" => 120, 
			_ => 15, 
		};
		return new PetActivity("after-" + prop, prop, (prop == "guitar") ? 1 : 0, -15.0, right, 0.0);
	}

	private void QaPose(PetTile tile)
	{
		QaMode = true;
		QaLife = new PetLife(7);
		base.ExpressionOverride = PetExpression.Normal;
		QaRocket = tile.Rocket;
		QaPlant = ((tile.Plant >= 0) ? tile.Plant : 20);
		QaStars = ((tile.Stars >= 0) ? tile.Stars : 12);
		QaFestival = tile.Festival;
		QaWeather = tile.Weather;
		QaOutcome = tile.Outcome;
		QaDrawing = tile.Drawing;
		if (QaRocket < 0)
		{
			QaRocket = 5;
		}
		scene = null;
		reaction = null;
		moment = null;
		activity = (incoming = null);
		segment = null;
		held.Clear();
		departed = false;
		pillow = (headphones = false);
		x = (previousX = 78.0);
		elapsed = 0.0;
		string hat = ((QaFestival == "christmas") ? "santa" : ((QaFestival == "birthday" || QaFestival == "newyear") ? "party" : null));
		string neck = ((QaWeather == "cold") ? "scarf" : null);
		if (rig == null)
		{
			rig = new PetVectorRig(shell, shade, ink)
			{
				Scale = 1.12,
				Ground = 38.5
			};
		}
		switch (tile.Kind)
		{
		case "activity":
		case "after":
		{
			activity = ((tile.Kind == "after") ? Aftermath(tile.Name) : PetContent.Activity(tile.Name));
			x = (previousX = WorkX(activity));
			QaLife.SetBase(activity.Name, activity.Sit, activity.Left, activity.Right, activity.Tilt);
			double num = 0.8;
			if (!string.IsNullOrEmpty(tile.Action))
			{
				QaLife.Start(tile.Action, 1.0, num);
			}
			QaLife.Step(1.0 + num * tile.U, allowed: true, 12);
			break;
		}
		case "micro":
			if (tile.Name == "page" || tile.Name == "sip")
			{
				activity = PetContent.Activity((tile.Name == "page") ? "reading" : "chai");
				x = (previousX = WorkX(activity));
				QaLife.SetBase(activity.Name, activity.Sit, activity.Left, activity.Right, activity.Tilt);
			}
			QaLife.Start(tile.Action, 1.0, 2.2);
			QaLife.Step(1.0 + 2.2 * tile.U, allowed: true, 12);
			break;
		case "festival":
			QaLife.Step(1.0, allowed: true, 12);
			break;
		case "big":
		case "drawing":
		{
			string name = ((tile.Kind == "drawing") ? "chalk" : tile.Name);
			string outcome = ((tile.Kind == "drawing") ? "erase" : tile.Outcome);
			scene = GardenScenes.Big(name, outcome, 78.0);
			elapsed = ((tile.Kind == "drawing") ? 8.6 : tile.Time);
			segment = new PetSegment
			{
				Kind = PetSegmentKind.Big,
				Name = name,
				Outcome = outcome,
				Drawing = tile.Drawing
			};
			break;
		}
		case "launch":
			scene = GardenScenes.Launch(78.0);
			elapsed = tile.Time;
			break;
		case "intro":
			scene = GardenScenes.FirstMeet();
			elapsed = tile.Time;
			break;
		case "intro-kick":
			introBox = false;
			scene = GardenScenes.IntroKick(140.0);
			elapsed = tile.Time;
			break;
		case "intro-hello":
		case "message":
		{
			if (tile.Kind == "intro-hello")
			{
				introBox = true;
			}
			else
			{
				activity = PetContent.Activity("reading");
				x = (previousX = WorkX(activity));
			}
			PetMoment petMoment2;
			if (!(tile.Kind == "intro-hello"))
			{
				PetMoment petMoment = new PetMoment();
				petMoment.Id = tile.Name;
				petMoment.Pose = tile.Action;
				petMoment.Title = tile.Outcome;
				petMoment.Expression = PetExpression.Happy;
				petMoment2 = petMoment;
			}
			else
			{
				PetMoment petMoment3 = new PetMoment();
				petMoment3.Id = "first-hello";
				petMoment3.Pose = "wave";
				petMoment3.Title = "Hi, I'm Ayn!";
				petMoment3.Expression = PetExpression.Happy;
				petMoment2 = petMoment3;
			}
			PetMoment next = petMoment2;
			Present(next);
			elapsed = ((tile.Time < 0.0) ? 4.0 : tile.Time);
			break;
		}
		}
		if (scene != null)
		{
			previousX = (x = Value("x", 78.0));
		}
		wearKey = null;
		RefreshWear(hat, neck);
		idleBodyKey = null;
		lastFrame = null;
		renderedFrame = null;
		eyesReady = false;
		renderedBubble = null;
	}

	private static Size StageSize(string style, double scale)
	{
		double num = style switch
		{
			"Island" => 240, 
			"Line" => 236, 
			"Compact" => 210, 
			_ => 228, 
		};
		double num2 = ((style == "Compact") ? 30 : ((style == "Line") ? 26 : 35));
		return new Size(Math.Round(num * scale), Math.Round(num2 * scale) + ((style == "Island") ? 5.0 : 0.0));
	}

	internal static BitmapSource RenderTile(PetTile tile, string style, double scale, double dpi)
	{
		Size size = StageSize(style, scale);
		PathGeometry pathGeometry = NotchGeometryBuilder.BuildGeometry(style, size.Width, size.Height).CloneCurrentValue();
		Rect bounds = pathGeometry.Bounds;
		pathGeometry.Transform = new TranslateTransform(0.0 - bounds.Left, 0.0 - bounds.Top);
		pathGeometry.Freeze();
		GardenView gardenView = new GardenView();
		gardenView.StageShape = pathGeometry;
		gardenView.Width = bounds.Width;
		gardenView.Height = bounds.Height;
		GardenView gardenView2 = gardenView;
		gardenView2.Measure(new Size(bounds.Width, bounds.Height));
		gardenView2.Arrange(new Rect(0.0, 0.0, bounds.Width, bounds.Height));
		gardenView2.QaPose(tile);
		if (gardenView2.DialogueWidth > bounds.Width)
		{
			pathGeometry = NotchGeometryBuilder.BuildGeometry(style, gardenView2.DialogueWidth, size.Height).CloneCurrentValue();
			bounds = pathGeometry.Bounds;
			pathGeometry.Transform = new TranslateTransform(0.0 - bounds.Left, 0.0 - bounds.Top);
			pathGeometry.Freeze();
			gardenView2.StageShape = pathGeometry;
			gardenView2.Width = bounds.Width;
			gardenView2.Height = bounds.Height;
			gardenView2.InvalidateStageClip();
			gardenView2.Measure(new Size(bounds.Width, bounds.Height));
			gardenView2.Arrange(new Rect(0.0, 0.0, bounds.Width, bounds.Height));
			gardenView2.QaPose(tile);
		}
		gardenView2.InvalidateVisual();
		gardenView2.UpdateLayout();
		RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap((int)Math.Ceiling(bounds.Width * dpi / 96.0), (int)Math.Ceiling(bounds.Height * dpi / 96.0), dpi, dpi, PixelFormats.Pbgra32);
		renderTargetBitmap.Render(gardenView2);
		renderTargetBitmap.Freeze();
		return renderTargetBitmap;
	}

	internal static string WriteContactSheet(string path, string style, double scale, double dpi)
	{
		List<PetTile> list = ContactTiles();
		List<BitmapSource> list2 = list.Select((PetTile t) => RenderTile(t, style, scale, dpi)).ToList();
		int num = list2.Max((BitmapSource i) => i.PixelWidth);
		int num2 = list2.Max((BitmapSource i) => i.PixelHeight);
		int num3 = Math.Max(1, 1500 / (num + 24));
		int num4 = (list.Count + num3 - 1) / num3;
		int num5 = num + 24;
		int num6 = num2 + 30;
		int num7 = 44;
		Typeface typeface = new Typeface("Segoe UI");
		SolidColorBrush solidColorBrush = new SolidColorBrush(Color.FromRgb(225, 225, 230));
		solidColorBrush.Freeze();
		DrawingVisual drawingVisual = new DrawingVisual();
		using (DrawingContext drawingContext = drawingVisual.RenderOpen())
		{
			drawingContext.DrawRectangle(new SolidColorBrush(Color.FromRgb(58, 58, 64)), null, new Rect(0.0, 0.0, num3 * num5 + 12, num4 * num6 + num7 + 8));
			drawingContext.DrawText(new FormattedText(string.Concat("Ayn pet ", typeof(GardenView).Assembly.GetName().Version, " | ", style, " ", scale * 100.0, "% | ", dpi, " dpi | stage ", StageSize(style, scale).Width, " x ", StageSize(style, scale).Height, " DIP | native pixels"), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 15.0, solidColorBrush, 1.0), new Point(12.0, 12.0));
			for (int j = 0; j < list.Count; j++)
			{
				double num8 = 12 + j % num3 * num5;
				double num9 = num7 + j / num3 * num6;
				drawingContext.DrawImage(list2[j], new Rect(num8, num9, list2[j].PixelWidth, list2[j].PixelHeight));
				drawingContext.DrawText(new FormattedText(j + 1 + ". " + list[j].Label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 11.0, solidColorBrush, 1.0), new Point(num8, num9 + (double)num2 + 3.0));
			}
		}
		RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(num3 * num5 + 12, num4 * num6 + num7 + 8, 96.0, 96.0, PixelFormats.Pbgra32);
		renderTargetBitmap.Render(drawingVisual);
		PngBitmapEncoder pngBitmapEncoder = new PngBitmapEncoder();
		pngBitmapEncoder.Frames.Add(BitmapFrame.Create(renderTargetBitmap));
		using (FileStream stream = File.Create(path))
		{
			pngBitmapEncoder.Save(stream);
		}
		return "{\"file\":\"" + System.IO.Path.GetFileName(path) + "\",\"style\":\"" + style + "\",\"scale\":" + scale.ToString(CultureInfo.InvariantCulture) + ",\"tilePixels\":[" + num + "," + num2 + "],\"tiles\":[" + string.Join(",", list.Select((PetTile t) => "\"" + t.Label + "\"")) + "]}";
	}
}
