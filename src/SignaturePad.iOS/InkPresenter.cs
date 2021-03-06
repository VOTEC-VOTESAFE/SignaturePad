﻿using System;
using System.Collections.Generic;
using CoreGraphics;
using Foundation;
using UIKit;

using System.Threading.Tasks;

namespace Xamarin.Controls
{
	partial class InkPresenter : UIView
	{
		static InkPresenter ()
		{
			ScreenDensity = (float)UIScreen.MainScreen.Scale;
		}

		public InkPresenter ()
			: base ()
		{
			Initialize ();
		}

		public InkPresenter (CGRect frame)
			: base (frame)
		{
			Initialize ();
		}

		private void Initialize ()
		{
			Opaque = false;
		}

		// If you put SignaturePad inside a ScrollView, this line of code prevent that the gesture inside 
		// an InkPresenter are dispatched to the ScrollView below
		public override bool GestureRecognizerShouldBegin (UIGestureRecognizer gestureRecognizer) => false;

        private bool singleTapNeeded = false;
		public override void TouchesBegan (NSSet touches, UIEvent evt)
		{
            singleTapNeeded = false;
			// create a new path and set the options
			currentPath = new InkStroke (UIBezierPath.Create (), new List<CGPoint> (), StrokeColor, StrokeWidth);

			// obtain the location of the touch
			var touch = touches.AnyObject as UITouch;
			var touchLocation = touch.LocationInView (this);

			// move the path to that position
			currentPath.Path.MoveTo (touchLocation);
			currentPath.GetPoints ().Add (touchLocation);

			// update the dirty rectangle
			ResetBounds (touchLocation);

			Console.WriteLine ("InkPresenter: Touches Began");

			if (touch != null)
			{
				Console.WriteLine ($"InkPresenter: TapCount: {touch.TapCount}");
				if (touch.TapCount == 2)
				{
					OnDoubleTapped ();
				}
			}
		}

		public override void TouchesMoved (NSSet touches, UIEvent evt)
		{
			// something may have happened (clear) so start the stroke again
			if (currentPath == null)
			{
				TouchesBegan (touches, evt);
			}

			// obtain the location of the touch
			var touch = touches.AnyObject as UITouch;
			var touchLocation = touch.LocationInView (this);

			if (HasMovedFarEnough (currentPath, touchLocation.X, touchLocation.Y))
			{
				// add it to the current path
				currentPath.Path.AddLineTo (touchLocation);
				currentPath.GetPoints ().Add (touchLocation);

				// update the dirty rectangle
				UpdateBounds (touchLocation);
				SetNeedsDisplayInRect (DirtyRect);
			}
		}

		public override void TouchesCancelled (NSSet touches, UIEvent evt)
		{
			TouchesEnded (touches, evt);
		}

		public override async void TouchesEnded (NSSet touches, UIEvent evt)
		{
			// obtain the location of the touch
			var touch = touches.AnyObject as UITouch;
			var touchLocation = touch.LocationInView (this);

			// something may have happened (clear) during the stroke
			if (currentPath != null)
			{
				if (HasMovedFarEnough (currentPath, touchLocation.X, touchLocation.Y))
				{
					// add it to the current path
					currentPath.Path.AddLineTo (touchLocation);
					currentPath.GetPoints ().Add (touchLocation);
				}
                else if (touch.TapCount == 1)
                {
					// Single Tap
					singleTapNeeded = true;
					await Task.Delay (300);
					if (singleTapNeeded)
					{
						//CGRect r = new CGRect (touchLocation.X, touchLocation.Y, StrokeWidth, StrokeWidth);
						//var c = UIBezierPath.FromOval (r);
						var s = new InkStroke(new UIBezierPath(), new List<CGPoint> (), StrokeColor, StrokeWidth);
						s.Path.MoveTo (touchLocation);
						s.GetPoints ().Add (touchLocation);
						var dest = new CGPoint (touchLocation.X + StrokeWidth, touchLocation.Y + StrokeWidth);
						s.Path.AddLineTo (dest);
						s.GetPoints ().Add (dest);
						paths.Add (s);

						s = new InkStroke (new UIBezierPath (), new List<CGPoint> (), StrokeColor, StrokeWidth);
						var src = new CGPoint (touchLocation.X + StrokeWidth, touchLocation.Y);
						s.Path.MoveTo (src);
						s.GetPoints ().Add (src);
						dest = new CGPoint (touchLocation.X, touchLocation.Y + StrokeWidth);
						s.Path.AddLineTo (dest);
						s.GetPoints ().Add (dest);
						paths.Add (s);

					};
                }

				// obtain the smoothed path, and add it to the old paths
				if (currentPath != null)
				{
					var smoothed = PathSmoothing.SmoothedPathWithGranularity (currentPath, 4);
					paths.Add (smoothed);
				}
			}

			// clear the current path
			currentPath = null;

			// update the dirty rectangle
			UpdateBounds (touchLocation);
			SetNeedsDisplay ();

			// we are done with drawing
			OnStrokeCompleted ();
		}

		public override void Draw (CGRect rect)
		{
			base.Draw (rect);

			// destroy an old bitmap
			if (bitmapBuffer != null && ShouldRedrawBufferImage)
			{
				var temp = bitmapBuffer;
				bitmapBuffer = null;

				temp.Dispose ();
				temp = null;
			}

			// re-create
			if (bitmapBuffer == null)
			{
				bitmapBuffer = CreateBufferImage ();
			}

			// if there are no lines, the the bitmap will be null
			if (bitmapBuffer != null)
			{
				bitmapBuffer.Draw (CGPoint.Empty);
			}

			// draw the current path over the old paths
			if (currentPath != null)
			{
				var context = UIGraphics.GetCurrentContext ();
				context.SetLineCap (CGLineCap.Round);
				context.SetLineJoin (CGLineJoin.Round);
				context.SetStrokeColor (currentPath.Color.CGColor);
				context.SetLineWidth (currentPath.Width);
				
				context.AddPath (currentPath.Path.CGPath);
				context.StrokePath ();
			}
		}

		private UIImage CreateBufferImage ()
		{
			if (paths == null || paths.Count == 0)
			{
				return null;
			}

			var size = Bounds.Size;
			UIGraphics.BeginImageContextWithOptions (size, false, ScreenDensity);
			var context = UIGraphics.GetCurrentContext ();

			context.SetLineCap (CGLineCap.Round);
			context.SetLineJoin (CGLineJoin.Round);

			foreach (var path in paths)
			{
				context.SetStrokeColor (path.Color.CGColor);
				context.SetLineWidth (path.Width);

				context.AddPath (path.Path.CGPath);
				context.StrokePath ();

				path.IsDirty = false;
			}

			var image = UIGraphics.GetImageFromCurrentImageContext ();

			UIGraphics.EndImageContext ();

			return image;
		}

		public override void LayoutSubviews ()
		{
			base.LayoutSubviews ();

			SetNeedsDisplay ();
		}
	}
}
