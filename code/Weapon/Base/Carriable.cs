﻿using Sandbox;

public partial class Carriable : BaseCarriable, IUse
{
	public virtual int Bucket => 1;
	public virtual int BucketWeight => 100;
	public virtual int Order => (Bucket * 1000) + BucketWeight;
	public virtual string WorldModelPath => "";
	public virtual string Icon => "";
	public virtual string DrawAnim => "deploy";
	public virtual string DrawEmptyAnim => null;
	public virtual bool EnableSwingAndBob => true;

	public override void Spawn()
	{
		base.Spawn();

		if ( !string.IsNullOrEmpty( WorldModelPath ) )
			SetModel( WorldModelPath );
	}

	/// <summary>
	/// This entity has become the active entity. This most likely
	/// means a player was carrying it in their inventory and now
	/// has it in their hands.
	/// </summary>
	public override void ActiveStart( Entity ent )
	{
		//base.ActiveStart( ent );

		EnableDrawing = true;

		if ( ent is Player player )
		{
			var animator = player.GetActiveAnimator();
			if ( animator != null )
			{
				SimulateAnimator( animator );
			}
		}

		if ( ent is NPC npc )
		{
			NPCAnimator( npc );
		}

		//
		// If we're the local player (clientside) create viewmodel
		// and any HUD elements that this weapon wants
		//
		if ( IsLocalPawn )
		{
			DestroyViewModel();
			DestroyHudElements();

			CreateViewModel();
			CreateHudElements();

			if ( this is Weapon wep )
			{
				if ( wep.AmmoClip <= 0 && !string.IsNullOrEmpty( DrawEmptyAnim ) )
					ViewModelEntity?.SetAnimParameter( DrawEmptyAnim, true );
				else if ( !string.IsNullOrEmpty( DrawAnim ) )
					ViewModelEntity?.SetAnimParameter( DrawAnim, true );
			}
			else if ( !string.IsNullOrEmpty( DrawAnim ) )
				ViewModelEntity?.SetAnimParameter( DrawAnim, true );
		}
	}

	public override void CreateViewModel()
	{
		Host.AssertClient();

		if ( string.IsNullOrEmpty( ViewModelPath ) )
			return;

		ViewModelEntity = new ViewModel
		{
			Position = Position,
			Owner = Owner,
			EnableViewmodelRendering = true,
			EnableSwingAndBob = EnableSwingAndBob
		};

		ViewModelEntity.SetModel( ViewModelPath );
	}

	public virtual void NPCAnimator( NPC npc )
	{
		npc.SetAnimParameter( "holdtype", 1 );
		npc.SetAnimParameter( "aim_body_weight", 1.0f );
		npc.SetAnimParameter( "holdtype_handedness", 0 );
	}

	public virtual bool OnUse( Entity user )
	{
		return false;
	}

	public virtual bool IsUsable( Entity user )
	{
		return Owner == null;
	}

	public virtual void RenderHud( in Vector2 screensize )
	{
		var draw = Render.Draw2D;
		var center = screensize * 0.5f;

		draw.BlendMode = BlendMode.Lighten;
		draw.Color = Color.White;

		var length = 4.0f;

		draw.Ring( center, length, length / 2 );
	}
}
