﻿using Sandbox;
using Sandbox.Tools;

[Library( "weapon_tool", Title = "Toolgun" )]
partial class Tool : Carriable
{
	[ConVar.ClientData( "tool_current" )]
	public static string UserToolCurrent { get; set; } = "tool_boxgun";

	public override string ViewModelPath => "weapons/rust_pistol/v_rust_pistol.vmdl";
	public override string WorldModelPath => "weapons/rust_pistol/rust_pistol.vmdl";

	[Net, Predicted]
	public BaseTool CurrentTool { get; set; }

	public override int Bucket => 5;
	public override string Icon => "ui/weapons/weapon_pistol.png";

	public override void Simulate( Client owner )
	{
		UpdateCurrentTool( owner );

		CurrentTool?.Simulate();
	}

	private void UpdateCurrentTool( Client owner )
	{
		var toolName = owner.GetClientData<string>( "tool_current", "tool_boxgun" );
		if ( toolName == null )
			return;

		// Already the right tool
		if ( CurrentTool != null && CurrentTool.Parent == this && CurrentTool.Owner == owner.Pawn && CurrentTool.ClassName == toolName )
			return;

		if ( CurrentTool != null )
		{
			CurrentTool?.Deactivate();
			CurrentTool = null;
		}

		CurrentTool = TypeLibrary.Create<BaseTool>( toolName );

		if ( CurrentTool != null )
		{
			CurrentTool.Parent = this;
			CurrentTool.Owner = owner.Pawn as Player;
			CurrentTool.Activate();
		}
	}

	public override void ActiveStart( Entity ent )
	{
		base.ActiveStart( ent );

		CurrentTool?.Activate();
	}

	public override void ActiveEnd( Entity ent, bool dropped )
	{
		base.ActiveEnd( ent, dropped );

		CurrentTool?.Deactivate();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();

		CurrentTool?.Deactivate();
		CurrentTool = null;
	}

	public override void OnCarryDrop( Entity dropper )
	{
	}

	[Event.Frame]
	public void OnFrame()
	{
		if ( Owner is Player player && player.ActiveChild != this )
			return;

		CurrentTool?.OnFrame();
	}

	public override void SimulateAnimator( PawnAnimator anim )
	{
		anim.SetAnimParameter( "holdtype", 1 );
		anim.SetAnimParameter( "aim_body_weight", 1.0f );
		anim.SetAnimParameter( "holdtype_handedness", 1 );
	}
}

namespace Sandbox.Tools
{
	public partial class BaseTool : BaseNetworkable
	{
		public Tool Parent { get; set; }
		public Player Owner { get; set; }

		protected virtual float MaxTraceDistance => 10000.0f;

		public virtual void Activate()
		{
			CreatePreviews();
		}

		public virtual void Deactivate()
		{
			DeletePreviews();
		}

		public virtual void Simulate()
		{

		}

		public virtual void OnFrame()
		{
			UpdatePreviews();
		}

		public virtual void CreateHitEffects( Vector3 pos )
		{
			Parent?.CreateHitEffects( pos );
		}
	}
}
