﻿using Sandbox;
using System.Collections.Generic;
using System.Linq;

public partial class NPC : AnimatedEntity
{
	public virtual float Speed => Rand.Float(100, 500);
	public virtual string ModelPath => "models/citizen/citizen.vmdl";
	public virtual float SpawnHealth => 0;
	public virtual bool HaveDress => true;
	public virtual float MeleeStrikeTime => 1f;
	public virtual bool UseWeapon => false;

	public NavSteer Steer;
	public ModelEntity Corpse;
	public bool IsHeadShot { get; private set; }

	DamageInfo lastDamage;
	TimeSince timeSinceJump;
	TimeSince timeSinceFall;
	TimeSince timeSinceMeleeStrike;

	public float NowSpeed { get; set; }

	[Predicted]
	Entity LastActiveChild { get; set; }

	[Net, Predicted]
	public Entity ActiveChild { get; set; }

	public IBaseInventory Inventory { get; protected set; }

	public override void Spawn()
	{
		base.Spawn();

		Inventory = new NPCInventory( this );
		Health = SpawnHealth;
		SetModel( ModelPath );
		EyePosition = Position + Vector3.Up * 64;
		CollisionGroup = CollisionGroup.Player;
		SetupPhysicsFromCapsule( PhysicsMotionType.Keyframed, Capsule.FromHeightAndRadius( 72, 8 ) );
		Tags.Add( "npc" );

		EnableHitboxes = true;

		if ( HaveDress )
			Dress();

		NowSpeed = Speed;
	}

	public override void OnKilled()
	{
		IsHeadShot = GetHitboxGroup( lastDamage.HitboxIndex ) == 1;

		BecomeRagdoll( Velocity, lastDamage.Flags, lastDamage.Position, lastDamage.Force, GetHitboxBone( lastDamage.HitboxIndex ) );

		base.OnKilled();

		Game.Current?.OnKilled( this );

		if ( lastDamage.Flags.HasFlag( DamageFlags.Vehicle ) )
		{
			Particles.Create( "particles/impact.flesh.bloodpuff-big.vpcf", lastDamage.Position );
			Particles.Create( "particles/impact.flesh-big.vpcf", lastDamage.Position );
			PlaySound( "kersplat" );
		}
	}

	public override void TakeDamage( DamageInfo info )
	{
		if ( GetHitboxGroup( info.HitboxIndex ) == 1 )
		{
			info.Damage *= 2.0f;
		}

		lastDamage = info;

		base.TakeDamage( info );

		if ( SpawnHealth <= 0 ) return;
		if ( info.Attacker != null && (info.Attacker is SandboxPlayer || info.Attacker.Owner is SandboxPlayer) )
		{
			SandboxPlayer attacker = info.Attacker as SandboxPlayer;

			if ( attacker == null )
				attacker = info.Attacker.Owner as SandboxPlayer;

			// Note - sending this only to the attacker!
			attacker.DidDamage( To.Single( attacker ), info.Position, info.Damage, Health.LerpInverse( 100, 0 ), Health <= 0 );
		}
	}

	public virtual void SimulateActiveChild( Entity child )
	{
		if ( LastActiveChild != child )
		{
			OnActiveChildChanged( LastActiveChild, child );
			LastActiveChild = child;
		}

		if ( !LastActiveChild.IsValid() )
			return;

		if ( LastActiveChild.IsAuthority )
		{
			LastActiveChild.Simulate( Client );
		}
	}

	public virtual void OnActiveChildChanged( Entity previous, Entity next )
	{
		if ( previous is BaseCarriable previousBc )
		{
			previousBc?.ActiveEnd( this, previousBc.Owner != this );
		}

		if ( next is BaseCarriable nextBc )
		{
			nextBc?.ActiveStart( this );
		}
	}

	public virtual IEnumerable<TraceResult> TraceBullet( Vector3 start, Vector3 end, float radius = 2.0f )
	{
		bool InWater = Map.Physics.IsPointWater( start );

		var tr = Trace.Ray( start, end )
				.UseHitboxes()
				.HitLayer( CollisionLayer.Water, !InWater )
				.HitLayer( CollisionLayer.Debris )
				.Ignore( Owner )
				.Ignore( this )
				.Size( radius )
				.Run();

		if ( tr.Hit )
			yield return tr;

		//
		// Another trace, bullet going through thin material, penetrating water surface?
		//
	}

	public void MeleeStrike( float damage, float force )
	{
		SetAnimParameter( "holdtype", 4 );

		// random swing attack
		if ( Rand.Int( 1 ) == 1 )
			SetAnimParameter( "holdtype_attack", 2.0f );
		else
			SetAnimParameter( "holdtype_attack", 1.0f );

		// random hand
		if ( Rand.Int( 1 ) == 1 )
			SetAnimParameter( "holdtype_handedness", 1 );
		else if ( Rand.Int( 2 ) == 1 )
			SetAnimParameter( "holdtype_handedness", 2 );
		else
			SetAnimParameter( "holdtype_handedness", 0 );

		SetAnimParameter( "b_attack", true );
		Velocity = 0;
		var forward = EyeRotation.Forward;
		forward = forward.Normal;

		var overlaps = FindInSphere( Position, 60 );

		if ( IsServer )
		{
			foreach ( var overlap in overlaps.OfType<Entity>().ToArray() )
			{
				if ( overlap == this ) continue;

				using ( Prediction.Off() )
				{
					var damageInfo = DamageInfo.FromBullet( overlap.Position, forward * 100 * force, damage )
						.WithAttacker( this );
					overlap.TakeDamage( damageInfo );

					// blood particles
					foreach ( var tr in TraceBullet( EyePosition, EyePosition, 100f ) )
					{
						tr.Surface.DoBulletImpact( tr );
					}
				}
			}
		}
	}

	private TraceResult StartTrace( Vector3 start, Vector3 end )
	{
		var tr = Sandbox.Trace.Ray( start, end )
			.Ignore( this )
			.HitLayer( CollisionLayer.All, false )
			.HitLayer( CollisionLayer.STATIC_LEVEL )
			.HitLayer( CollisionLayer.Solid )
			.Run();

		return tr;
	}

	Vector3 InputVelocity;
	Vector3 LookDir;

	public virtual void MoveTick()
    {
		InputVelocity = 0;

		if ( Steer != null )
		{
			Steer.Tick( Position );

			if ( !Steer.Output.Finished )
			{
				InputVelocity = Steer.Output.Direction.Normal;
				Velocity = Velocity.AddClamped( InputVelocity * Time.Delta * 500, NowSpeed );

				var distance = 60f;
				var start = Position;
				var end = Position + Rotation.Forward * distance;
				var tr = StartTrace( start, end );

				if ( tr.Hit )
				{
					start = Position;
					end = Position + Vector3.Up * 5;
					tr = StartTrace( start, end );

					if ( !tr.Hit )
					{
						start = tr.EndPosition;
						end = tr.EndPosition + Rotation.Forward * distance + 5;
						tr = StartTrace( start, end );

						if ( tr.Hit )
						{
							start = Position;
							end = Position + Vector3.Up * 64;
							tr = StartTrace( start, end );

							if ( !tr.Hit )
							{
								start = tr.EndPosition;
								end = tr.EndPosition + Rotation.Forward * distance + 5;
								tr = StartTrace( start, end );

								if ( !tr.Hit && timeSinceJump > 1f )
								{
									float flGroundFactor = 1.0f;
									float flMul = 268.3281572999747f * 1.2f;
									float startz = Velocity.z;

									timeSinceJump = 0f;
									GroundEntity = null;
									Position += Vector3.Up * 5;
									Velocity = Velocity.WithZ( startz + flMul * flGroundFactor );

									SetAnimParameter( "b_jump", true );
								}
							}
						}
					}
				}
			}
		}

		Move( Time.Delta );

		var walkVelocity = Velocity.WithZ( 0 );
		if ( walkVelocity.Length > 0.5f )
		{
			var turnSpeed = walkVelocity.Length.LerpInverse( 0, 100, true );
			var targetRotation = Rotation.LookAt( walkVelocity.Normal, Vector3.Up );
			Rotation = Rotation.Lerp( Rotation, targetRotation, turnSpeed * Time.Delta * 20.0f );
		}

		var animHelper = new CitizenAnimationHelper( this );

		LookDir = Vector3.Lerp( LookDir, InputVelocity.WithZ( 0 ) * 1000, Time.Delta * 100.0f );
		EyeRotation = Rotation;

		animHelper.WithLookAt( EyePosition + LookDir );
		animHelper.WithVelocity( Velocity );
		animHelper.WithWishVelocity( InputVelocity );

		var DownVel = Velocity * Rotation.Down;
		var falldamage = DownVel.z / 50;

		if ( timeSinceFall > 0.05f && DownVel.z > 750 && GroundEntity != null )
		{
			timeSinceFall = 0;

			var dmg = new DamageInfo()
			{
				Position = Position,
				Damage = falldamage
			};

			//PlaySound("dm.ui_attacker");
			TakeDamage( dmg );
		}

		if ( timeSinceMeleeStrike > MeleeStrikeTime )
		{
			timeSinceMeleeStrike = 0f;

			SetAnimParameter( "holdtype", 0 );
			DoMeleeStrike();
		}
	}

	public virtual void NPCAnimator()
	{
		if ( ActiveChild is Carriable carry )
		{
			carry.NPCAnimator( this );
		}
		else
		{
			SetAnimParameter( "holdtype", 0 );
			SetAnimParameter( "aim_body_weight", 0.5f ); // old
			SetAnimParameter( "aim_body_weight", 0.5f );
		}
	}

	public virtual void OnTick()
    {

    }

	public virtual void DoMeleeStrike()
	{

	}

	[Event.Tick.Server]
	public void Tick()
	{
		MoveTick();
		OnTick();
		SimulateActiveChild( ActiveChild );
		NPCAnimator();
	}

	protected virtual void Move( float timeDelta )
	{
		var bbox = BBox.FromHeightAndRadius( 64, 4 );

		MoveHelper move = new( Position, Velocity );
		move.MaxStandableAngle = 50;
		move.Trace = move.Trace.Ignore( this ).Size( bbox );

		if ( !Velocity.IsNearlyZero( 0.001f ) )
		{
			move.TryUnstuck();
			move.TryMoveWithStep( timeDelta, 30 );
		}

		var tr = move.TraceDirection( Vector3.Down * 10.0f );

		if ( move.IsFloor( tr ) )
		{
			GroundEntity = tr.Entity;

			if ( !tr.StartedSolid )
			{
				move.Position = tr.EndPosition;
			}

			if ( InputVelocity.Length > 0 )
			{
				var movement = move.Velocity.Dot( InputVelocity.Normal );
				move.Velocity = move.Velocity - movement * InputVelocity.Normal;
				move.ApplyFriction( tr.Surface.Friction * 10.0f, timeDelta );
				move.Velocity += movement * InputVelocity.Normal;
			}
			else
			{
				move.ApplyFriction( tr.Surface.Friction * 10.0f, timeDelta );
			}
		}
		else
		{
			GroundEntity = null;
			move.Velocity += Vector3.Down * 900 * timeDelta;
		}

		Position = move.Position;
		Velocity = move.Velocity;
	}
}
