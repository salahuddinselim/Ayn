using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using WindowsBuddy.Services.Pet;

namespace WindowsBuddy.BuddyCapsule.Renderer;

internal sealed class PetVectorRig
{
	private readonly DrawingGroup drawing = new DrawingGroup();

	private readonly DrawingGroup eyes = new DrawingGroup();

	private readonly DrawingGroup hatWear = new DrawingGroup();

	private readonly DrawingGroup neckWear = new DrawingGroup();

	private readonly MatrixTransform footL = new MatrixTransform();

	private readonly MatrixTransform footR = new MatrixTransform();

	private readonly MatrixTransform torso = new MatrixTransform();

	private readonly MatrixTransform armL = new MatrixTransform();

	private readonly MatrixTransform armR = new MatrixTransform();

	private readonly MatrixTransform head = new MatrixTransform();

	private readonly MatrixTransform eyePosition = new MatrixTransform();

	private readonly Dictionary<PetExpression, DrawingGroup> expressions = new Dictionary<PetExpression, DrawingGroup>();

	private PetExpression? expression;

	private DrawingGroup frontL;

	private DrawingGroup frontR;

	public double Scale = 1.0;

	public double Ground = 37.0;

	private static DrawingGroup Group(DrawingGroup parent, Transform transform)
	{
		DrawingGroup drawingGroup = new DrawingGroup();
		drawingGroup.Transform = transform;
		DrawingGroup drawingGroup2 = drawingGroup;
		parent.Children.Add(drawingGroup2);
		return drawingGroup2;
	}

	private static void Box(DrawingGroup parent, Brush brush, double x, double y, double w, double h, double r)
	{
		RectangleGeometry rectangleGeometry = new RectangleGeometry(new Rect(x, y, w, h), r, r);
		rectangleGeometry.Freeze();
		GeometryDrawing geometryDrawing = new GeometryDrawing(brush, null, rectangleGeometry);
		geometryDrawing.Freeze();
		parent.Children.Add(geometryDrawing);
	}

	public PetVectorRig(Brush shell, Brush shade, Brush ink)
	{
		Box(Group(drawing, footL), shade, 7.0, 26.0, 6.0, 4.0, 2.0);
		Box(Group(drawing, footR), shade, 15.0, 26.0, 6.0, 4.0, 2.0);
		DrawingGroup drawingGroup = Group(drawing, torso);
		Box(drawingGroup, shade, 8.0, 18.0, 11.0, 10.0, 4.0);
		Box(drawingGroup, shell, 8.0, 17.0, 10.0, 10.0, 4.0);
		drawingGroup.Children.Add(neckWear);
		Box(Group(drawing, armL), shell, 5.0, 18.0, 4.0, 8.0, 2.0);
		Box(Group(drawing, armR), shell, 18.0, 18.0, 4.0, 8.0, 2.0);
		DrawingGroup drawingGroup2 = Group(drawing, head);
		Box(drawingGroup2, shade, 2.0, 2.0, 23.0, 17.0, 5.0);
		Box(drawingGroup2, shell, 2.0, 1.0, 23.0, 17.0, 5.0);
		Geometry geometry = Geometry.Parse("M 6,1 L 21,1 L 21,8 Q 21,12 17,12 L 10,12 Q 6,12 6,8 Z");
		geometry.Freeze();
		GeometryDrawing geometryDrawing = new GeometryDrawing(ink, null, geometry);
		geometryDrawing.Freeze();
		drawingGroup2.Children.Add(geometryDrawing);
		drawingGroup2.Children.Add(hatWear);
		frontL = Group(drawing, armL);
		frontR = Group(drawing, armR);
		Box(frontL, shell, 5.0, 18.0, 4.0, 15.0, 2.0);
		Box(frontR, shell, 18.0, 18.0, 4.0, 15.0, 2.0);
		frontL.Opacity = (frontR.Opacity = 0.0);
	}

	public void SetWear(string hat, string neck, bool colours, bool headphones = false)
	{
		using (DrawingContext drawingContext = hatWear.Open())
		{
			if (headphones)
			{
				Pen pen = new Pen(Paint(244, 241, 234), 2.6);
				pen.StartLineCap = PenLineCap.Round;
				pen.EndLineCap = PenLineCap.Round;
				Pen pen2 = pen;
				pen2.Freeze();
				Geometry geometry = Geometry.Parse("M 2.5,9 C 2.5,-5 24.5,-5 24.5,9");
				geometry.Freeze();
				drawingContext.DrawGeometry(null, pen2, geometry);
				Brush brush = Paint(105, 201, 232);
				drawingContext.DrawRoundedRectangle(brush, null, new Rect(-1.5, 5.0, 6.0, 9.0), 2.5, 2.5);
				drawingContext.DrawRoundedRectangle(brush, null, new Rect(22.5, 5.0, 6.0, 9.0), 2.5, 2.5);
			}
			if (hat == "santa")
			{
				Geometry geometry2 = Geometry.Parse("M 6,2 Q 12,-8 21,-3 L 21,2 Z");
				geometry2.Freeze();
				drawingContext.DrawGeometry(Paint(229, 57, 53), null, geometry2);
				drawingContext.DrawRoundedRectangle(Brushes.White, null, new Rect(5.0, 0.0, 17.0, 3.0), 1.5, 1.5);
				drawingContext.DrawEllipse(Brushes.White, null, new Point(21.5, -3.0), 2.0, 2.0);
			}
			else if (hat == "party")
			{
				Geometry geometry3 = Geometry.Parse("M 9,2 L 13.5,-9 L 18,2 Z");
				geometry3.Freeze();
				drawingContext.DrawGeometry(Paint(105, 201, 232), null, geometry3);
				drawingContext.DrawLine(new Pen(Paint(byte.MaxValue, 213, 79), 1.0), new Point(11.0, -2.0), new Point(16.0, -1.0));
				drawingContext.DrawEllipse(Paint(byte.MaxValue, 213, 79), null, new Point(13.5, -9.5), 1.5, 1.5);
			}
			if (colours)
			{
				drawingContext.DrawEllipse(Paint(244, 143, 177), null, new Point(4.5, 14.0), 1.6, 1.3);
				drawingContext.DrawEllipse(Paint(byte.MaxValue, 213, 79), null, new Point(22.0, 3.5), 1.4, 1.2);
				drawingContext.DrawEllipse(Paint(129, 199, 132), null, new Point(19.0, 15.5), 1.2, 1.0);
			}
		}
		using DrawingContext drawingContext2 = neckWear.Open();
		if (neck == "scarf")
		{
			Brush brush2 = Paint(229, 115, 115);
			drawingContext2.DrawRoundedRectangle(brush2, null, new Rect(7.0, 16.5, 13.0, 3.0), 1.5, 1.5);
			drawingContext2.DrawRoundedRectangle(brush2, null, new Rect(15.0, 18.0, 3.0, 7.0), 1.5, 1.5);
		}
	}

	private static Brush Paint(byte r, byte g, byte b)
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush(Color.FromRgb(r, g, b));
		solidColorBrush.Freeze();
		return solidColorBrush;
	}

	private DrawingGroup Eyes(PetExpression value)
	{
		if (expressions.TryGetValue(value, out var value2))
		{
			return value2;
		}
		value2 = new DrawingGroup();
		using (DrawingContext drawingContext = value2.Open())
		{
			for (int i = 0; i < 2; i++)
			{
				double num = 10.5 + (double)(i * 6);
				Pen pen = new Pen(Brushes.White, 0.81);
				pen.StartLineCap = PenLineCap.Round;
				pen.EndLineCap = PenLineCap.Round;
				Pen pen2 = pen;
				pen2.Freeze();
				if (value == PetExpression.Happy)
				{
					StreamGeometry streamGeometry = new StreamGeometry();
					using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
					{
						streamGeometryContext.BeginFigure(new Point(num - 1.5, 6.5), isFilled: false, isClosed: false);
						streamGeometryContext.QuadraticBezierTo(new Point(num, 2.75), new Point(num + 1.5, 6.5), isStroked: true, isSmoothJoin: false);
					}
					streamGeometry.Freeze();
					drawingContext.DrawGeometry(null, pen2, streamGeometry);
					continue;
				}
				if (value == PetExpression.Sleepy || value == PetExpression.Closed || (value == PetExpression.Wink && i == 0))
				{
					drawingContext.DrawLine(pen2, new Point(num - 1.2, 6.0), new Point(num + 1.2, 6.0));
					continue;
				}
				if (value == PetExpression.Worried)
				{
					drawingContext.PushTransform(new RotateTransform((i == 0) ? 12 : (-12), num, 6.0));
				}
				double num2 = ((value == PetExpression.Surprised) ? 7 : 5);
				drawingContext.DrawRoundedRectangle(Brushes.White, null, new Rect(num - 1.5, 6.0 - num2 / 2.0, 3.0, num2), 1.5, 1.5);
				if (value == PetExpression.Worried)
				{
					drawingContext.Pop();
				}
			}
		}
		value2.Freeze();
		expressions.Add(value, value2);
		return value2;
	}

	private static void Set(MatrixTransform transform, Matrix value)
	{
		if (transform.Matrix != value)
		{
			transform.Matrix = value;
		}
	}

	public void Draw(DrawingContext d, double x, double jump, double gait, double sitting, double sleeping, double left, double right, double tilt, Vector look, PetExpression mood, double closure, bool drawBody = true, bool liveEyes = true, double breath = 0.0, double eyeScale = 1.0, double spin = 0.0, double headLift = 0.0)
	{
		double num = ((jump > 0.0) ? (jump * 0.035) : ((jump < 0.0) ? (jump * 0.008) : 0.0));
		Matrix identity = Matrix.Identity;
		identity.RotateAt(spin, 13.0, 15.0);
		identity.RotateAt(sleeping * 80.0, 13.0, 19.0);
		identity.ScaleAt(1.0 + num, 1.0 - num, 13.0, 30.0);
		identity.ScaleAt(Scale, Scale, 13.0, 30.0);
		identity.Translate(x - 13.0, Ground - 30.0 + jump);
		Matrix identity2 = Matrix.Identity;
		identity2.RotateAt(gait * 1.5, 13.0, 28.0);
		identity2.Translate(0.0, 0.0 - Math.Abs(gait) + breath);
		identity2.Append(identity);
		Matrix identity3 = Matrix.Identity;
		identity3.Translate(0.0, sitting * 3.0 + headLift);
		identity3.RotateAt(tilt, 13.0, 14.0);
		identity3.Append(identity2);
		Matrix identity4 = Matrix.Identity;
		if (drawBody)
		{
			identity4.Translate(0.0, (0.0 - Math.Max(0.0, gait)) * 2.4);
			identity4.Append(identity);
			Set(footL, identity4);
			identity4 = Matrix.Identity;
			identity4.Translate(0.0, (0.0 - Math.Max(0.0, 0.0 - gait)) * 2.4);
			identity4.Append(identity);
			Set(footR, identity4);
			identity4 = Matrix.Identity;
			identity4.ScaleAt(1.0, 1.0 - sitting * 0.2, 13.0, 18.0);
			identity4.Append(identity2);
			Set(torso, identity4);
			identity4 = Matrix.Identity;
			identity4.RotateAt(0.0 - left, 7.0, 19.0);
			identity4.Append(identity2);
			Set(armL, identity4);
			identity4 = Matrix.Identity;
			identity4.RotateAt(0.0 - right, 20.0, 19.0);
			identity4.Append(identity2);
			Set(armR, identity4);
			Set(head, identity3);
			double num2 = ((0.0 - left > 100.0) ? 1 : 0);
			double num3 = ((right > 100.0) ? 1 : 0);
			if (frontL.Opacity != num2)
			{
				frontL.Opacity = num2;
			}
			if (frontR.Opacity != num3)
			{
				frontR.Opacity = num3;
			}
		}
		if (liveEyes)
		{
			identity4 = Matrix.Identity;
			identity4.ScaleAt(1.0, Math.Max(0.14, (1.0 - closure) * eyeScale), 13.0, 6.0);
			identity4.Translate(look.X * 1.7, look.Y);
			identity4.Append(identity3);
			Set(eyePosition, identity4);
			if (expression != mood)
			{
				expression = mood;
				eyes.Children.Clear();
				eyes.Children.Add(Eyes(mood));
			}
		}
		if (drawBody)
		{
			d?.DrawDrawing(drawing);
		}
		if (liveEyes && d != null)
		{
			d.PushTransform(eyePosition);
			d.DrawDrawing(eyes);
			d.Pop();
		}
	}
}
