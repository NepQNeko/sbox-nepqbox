﻿using Sandbox;
using SandboxEditor;
using System.Linq;

namespace Gamelib.DayNight
{
	public class DayNightGradient
	{
		private struct GradientNode
		{
			public Color Color;
			public float Time;

			public GradientNode( Color color, float time )
			{
				Color = color;
				Time = time;
			}
		}

		private GradientNode[] _nodes;

		public DayNightGradient( Color dawnColor, Color dayColor, Color duskColor, Color nightColor )
		{
			_nodes = new GradientNode[7];
			_nodes[0] = new GradientNode( nightColor, 0f );
			_nodes[1] = new GradientNode( nightColor, 0.2f );
			_nodes[2] = new GradientNode( dawnColor, 0.3f );
			_nodes[3] = new GradientNode( dayColor, 0.5f );
			_nodes[4] = new GradientNode( dayColor, 0.7f );
			_nodes[5] = new GradientNode( duskColor, 0.85f );
			_nodes[6] = new GradientNode( nightColor, 1f );
		}

		public Color Evaluate( float fraction )
		{
			for ( var i = 0; i < _nodes.Length; i++ )
			{
				var node = _nodes[i];
				var nextIndex = i + 1;

				if ( _nodes.Length < nextIndex )
					nextIndex = 0;

				var nextNode = _nodes[nextIndex];

				if ( fraction >= node.Time && fraction <= nextNode.Time )
				{
					var duration = (nextNode.Time - node.Time);
					var interpolate = (1f / duration) * (fraction - node.Time);

					return Color.Lerp( node.Color, nextNode.Color, interpolate );
				}
			}

			return _nodes[0].Color;
		}
	}

	/// <summary>
	/// A way to set the colour based on the time of day, it will smoothly blend between each colour when the time has changed. Also enables the day night cycle using a "light_environment".
	/// </summary>
	[Library( "daynight_controller" )]
	[Title( "Day and Night Controller" )]
	[EditorSprite( "editor/daynight_controller.vmat" )]
	public partial class DayNightController : ModelEntity
	{
		[Property( "DawnColor", Title = "Dawn Color" )]
		public Color DawnColor { get; set; }

		[Property( "DawnSkyColor", Title = "Dawn Sky Color" )]
		public Color DawnSkyColor { get; set; }

		[Property( "DayColor", Title = "Day Color" )]
		public Color DayColor { get; set; }

		[Property( "DaySkyColor", Title = "Day Sky Color" )]
		public Color DaySkyColor { get; set; }

		[Property( "DuskColor", Title = "Dusk Color" )]
		public Color DuskColor { get; set; }
		[Property( "DuskSkyColor", Title = "Dusk Sky Color" )]
		public Color DuskSkyColor { get; set; }

		[Property( "NightColor", Title = "Night Color" )]
		public Color NightColor { get; set; }

		[Property( "NightSkyColor", Title = "Night Sky Color" )]
		public Color NightSkyColor { get; set; }

		protected Output OnBecomeNight { get; set; }
		protected Output OnBecomeDusk { get; set; }
		protected Output OnBecomeDawn { get; set; }
		protected Output OnBecomeDay { get; set; }

		public bool Enable = true;

		public EnvironmentLightEntity Environment
		{
			get
			{
				if ( _environment == null )
					_environment = All.OfType<EnvironmentLightEntity>().FirstOrDefault();
				return _environment;
			}
		}

		public Sky Sky
		{
			get
			{
				if ( _sky == null )
					_sky = All.OfType<Sky>().FirstOrDefault();
				return _sky;
			}
		}

		private Sky _sky;
		private EnvironmentLightEntity _environment;
		private DayNightGradient _skyColorGradient;
		private DayNightGradient _colorGradient;

		public override void Spawn()
		{
			_colorGradient = new DayNightGradient( DawnColor, DayColor, DuskColor, NightColor );
			_skyColorGradient = new DayNightGradient( DawnSkyColor, DaySkyColor, DuskSkyColor, NightSkyColor );

			DayNightManager.OnSectionChanged += HandleTimeSectionChanged;

			base.Spawn();
		}

		public void SetColors()
		{
			_colorGradient = new DayNightGradient( DawnColor, DayColor, DuskColor, NightColor );
			_skyColorGradient = new DayNightGradient( DawnSkyColor, DaySkyColor, DuskSkyColor, NightSkyColor );
		}

		private void HandleTimeSectionChanged( TimeSection section )
		{
			if ( section == TimeSection.Dawn )
				OnBecomeDawn.Fire( this );
			else if ( section == TimeSection.Day )
				OnBecomeDay.Fire( this );
			else if ( section == TimeSection.Dusk )
				OnBecomeDusk.Fire( this );
			else if ( section == TimeSection.Night )
				OnBecomeNight.Fire( this );
		}

		private bool IsSun;

		[Event.Tick.Server]
		private void Tick()
		{
			var environment = Environment;
			if ( environment == null ) return;

			var sunAngle = ((DayNightManager.TimeOfDay / 24f) * 360f);
			var tempmoonAngle = sunAngle + 180;
			var radius = 10000f;
			var changeAngle = 300f;

			float moonAngle;
			if ( tempmoonAngle < 360 )
				moonAngle = tempmoonAngle;
			else
				moonAngle = tempmoonAngle - 360;

			var sunmoonAngle = moonAngle;

			if ( IsSun ) sunmoonAngle = sunAngle;
			if ( !IsSun && moonAngle > changeAngle )
			{
				IsSun = true;
			}

			if ( IsSun && sunAngle > changeAngle )
			{
				IsSun = false;
			}

			if ( !Enable ) return;

			environment.Color = _colorGradient.Evaluate( (1f / 24f) * DayNightManager.TimeOfDay );
			environment.SkyColor = _skyColorGradient.Evaluate( (1f / 24f) * DayNightManager.TimeOfDay );

			environment.Position = Vector3.Zero + Rotation.From( 0, 0, sunmoonAngle + 60f ) * (radius * Vector3.Right);
			environment.Position += Rotation.From( 0, sunmoonAngle, 0 ) * (radius * Vector3.Forward);

			if ( NepQBoxGame.DayNightCycleDebug )
				DebugOverlay.Sphere( environment.Position, 2000f, Color.Yellow );

			var direction = (Vector3.Zero - environment.Position).Normal;
			environment.Rotation = Rotation.LookAt( direction, Vector3.Up );

			if ( NepQBoxGame.DayNightCycleDebug )
				DebugOverlay.Line( environment.Position, environment.Position + environment.Rotation.Forward * 10000f, Color.Blue );

			//environment.SkyIntensity = 0f;
			//environment.Brightness = 0f;

			var sky = Sky;
			if ( sky == null )
				return;

			//SetSky( sky, environment.Position, environment.SkyColor );
		}

		[ClientRpc]
		private void SetSky( Sky sky, Vector3 pos, Color color )
		{
			var skyobj = sky.SkyObject;
			//SceneSkyBox.SkyLightInfo[] lightinfo = new {
			//	new SceneSkyBox.SkyLightInfo
			//	{
			//		LightColor = color,
			//		LightDirection = pos
			//	},
			//	new SceneSkyBox.SkyLightInfo
			//	{
			//		LightColor = color,
			//		LightDirection = pos
			//	}
			//};

			//string[] test = new { "1", "2" };

			skyobj.SkyTint = color;

			//skyobj.SetSkyLighting( pos );
		}
	}
}
