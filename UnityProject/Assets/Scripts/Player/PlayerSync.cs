using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;



/// <summary>
/// Holds player state information used for interpolation, such as player position, flight direction etc.
/// Gives client enough information to be able to smoothly interpolate the player position.
/// </summary>
public struct PlayerState
{
	public bool Active => Position != TransformState.HiddenPos;
	///Don't set directly, use Speed instead.
	///public in order to be serialized :\
	public float speed;
	public float Speed {
		get => speed;
		set => speed = value < 0 ? 0 : value;
	}

	public int MoveNumber;
	public Vector3 Position;

	public Vector3 WorldPosition
	{
		get
		{
			if (!Active)
			{
				return TransformState.HiddenPos;
			}
			return MatrixManager.LocalToWorld(Position, MatrixManager.Get(MatrixId));
		}
		set
		{
			if (value == TransformState.HiddenPos)
			{
				Position = TransformState.HiddenPos;
			}
			else
			{
				Position = MatrixManager.WorldToLocal(value, MatrixManager.Get(MatrixId));
			}
		}
	}

	/// Flag means that this update is a pull follow update,
	/// So that puller could ignore them
	public bool IsFollowUpdate;

	public bool NoLerp;

	///Direction of flying
	public Vector2 Impulse;

	///Flag for clients to reset their queue when received
	public bool ResetClientQueue;

	/// Flag for server to ensure that clients receive that flight update:
	/// Only important flight updates (ones with impulse) are being sent out by server (usually start only)
	[NonSerialized] public bool ImportantFlightUpdate;

	public int MatrixId;

	/// Means that this player is hidden
	public static readonly PlayerState HiddenState =
		new PlayerState { Position = TransformState.HiddenPos, MatrixId = 0 };

	public override string ToString()
	{
		return
			Equals(HiddenState) ? "[Hidden]" : $"[Move #{MoveNumber}, localPos:{(Vector2)Position}, worldPos:{(Vector2)WorldPosition} {nameof(NoLerp)}:{NoLerp}, {nameof(Impulse)}:{Impulse}, " +
			$"reset: {ResetClientQueue}, flight: {ImportantFlightUpdate}, follow: {IsFollowUpdate}, matrix #{MatrixId}]";
	}
}

public partial class PlayerSync : NetworkBehaviour, IPushable
{
	/// <summary>
	/// Player is huge, okay?
	/// </summary>
	public ItemSize Size => ItemSize.Huge;

	public bool IsTileSnap { get; } = true;

	///For server code. Contains position
	public PlayerState ServerState => serverState;

	/// For client code
	public PlayerState ClientState => playerState;

	/// <summary>
	/// Returns whether player is currently moving. Returns correct value depending on if this
	/// is being called from client or server.
	/// </summary>
	public bool IsMoving => isServer ? IsMovingServer : IsMovingClient;

	public PlayerMove playerMove;
	private PlayerScript playerScript;
	private Directional playerDirectional;

	private Matrix Matrix => registerTile.Matrix;

	private RaycastHit2D[] rayHit;

	//		private float pullJourney;
	/// <summary>
	/// Note - can be null if this is a ghost player
	/// </summary>
	private PushPull pushPull;
	public bool IsBeingPulledServer => pushPull && pushPull.IsBeingPulled;
	public bool IsBeingPulledClient => pushPull && pushPull.IsBeingPulledClient;

	private RegisterTile registerTile;

	public void Nudge( NudgeInfo info ){}

	/// <summary>
	/// Checks both directions of a diagonal movement
	/// to see if movement or a bump resulting in an interaction is possible. Modifies
	/// the move action to switch to that direction, otherwise leaves it unmodified.
	/// Prioritizes the following when there are multiple options:
	/// 1. Move into empty space if either direction has it
	/// 2. Swap with a player if we and they have help intent
	/// 3. Push an object if either direction has it
	/// 4. Open a door if either direction has it
	///
	/// When both directions have the same condition (both doors or pushable objects), x will be preferred to y
	/// </summary>
	/// <param name="state">current state to try to slide from</param>
	/// <param name="action">current player action (which should have a diagonal movement). Will be modified if a slide is performed</param>
	/// <returns>bumptype of the final direction of movement if action is modified. Null otherwise</returns>
	private BumpType? TrySlide(PlayerState state, bool isServer, ref PlayerAction action)
	{
		Vector2Int direction = action.Direction();
		if (Math.Abs(direction.x) + Math.Abs(direction.y) < 2)
		{
			//not diagonal, do nothing
			return null;
		}

		Vector2Int xDirection = new Vector2Int(direction.x, 0);
		Vector2Int yDirection = new Vector2Int(0, direction.y);
		BumpType xBump = MatrixManager.GetBumpTypeAt(state.WorldPosition.RoundToInt(), xDirection, playerMove, isServer);
		BumpType yBump = MatrixManager.GetBumpTypeAt(state.WorldPosition.RoundToInt(), yDirection, playerMove, isServer);

		MoveAction? newAction = null;
		BumpType? newBump = null;

		if (xBump == BumpType.None || xBump == BumpType.HelpIntent)
		{
			newAction = PlayerAction.GetMoveAction(xDirection);
			newBump = xBump;
		}
		else if (yBump == BumpType.None || yBump == BumpType.HelpIntent)
		{
			newAction = PlayerAction.GetMoveAction(yDirection);
			newBump = yBump;
		}
		else if (xBump == BumpType.Push)
		{
			newAction = PlayerAction.GetMoveAction(xDirection);
			newBump = xBump;
		}
		else if (yBump == BumpType.Push)
		{
			newAction = PlayerAction.GetMoveAction(yDirection);
			newBump = yBump;
		}
		else if (xBump == BumpType.ClosedDoor)
		{
			newAction = PlayerAction.GetMoveAction(xDirection);
			newBump = xBump;
		}
		else if (yBump == BumpType.ClosedDoor)
		{
			newAction = PlayerAction.GetMoveAction(yDirection);
			newBump = yBump;
		}

		if (newAction.HasValue)
		{
			action.moveActions = new int[] { (int)newAction };
			return newBump;
		}
		else
		{
			return null;
		}
	}

	/// <summary>
	/// Checks if any bump would occur with the movement in the specified action.
	/// If a BumpType.Blocked occurs, attempts to slide (if movement is diagonal). Updates
	/// playerAction's movement if slide occurs.
	/// </summary>
	/// <param name="playerState">state moving from</param>
	/// <param name="playerAction">action indicating the movement, will be modified if slide occurs</param>
	/// <returns>the type of bump that occurs at the final destination (after sliding has been attempted)</returns>
	private BumpType CheckSlideAndBump(PlayerState playerState, bool isServer, ref PlayerAction playerAction)
	{
		//bump never happens if we are a ghost
		if (playerScript.IsGhost)
		{
			return BumpType.None;
		}
		BumpType bump = MatrixManager.GetBumpTypeAt(playerState, playerAction, playerMove, isServer);
		// if movement is blocked, try to slide
		if (bump == BumpType.Blocked)
		{
			return TrySlide(playerState, isServer, ref playerAction) ?? bump;
		}

		return bump;
	}

	#region spess interaction logic

	private bool IsAroundPushables(PlayerState state, bool isServer)
	{
		PushPull pushable;
		return IsAroundPushables(state, isServer, out pushable);
	}

	/// Around player
	private bool IsAroundPushables(PlayerState state, bool isServer, out PushPull pushable, GameObject except = null)
	{
		return IsAroundPushables(state.WorldPosition, isServer, out pushable, except);
	}

	/// Man, these are expensive and generate a lot of garbage. Try to use sparsely
	private bool IsAroundPushables(Vector3 worldPos, bool isServer, out PushPull pushable, GameObject except = null)
	{
		pushable = null;
		foreach (Vector3Int pos in worldPos.CutToInt().BoundsAround().allPositionsWithin)
		{
			if (HasPushablesAt(pos, isServer, out pushable, except))
			{
				return true;
			}
		}

		return false;
	}

	private bool HasPushablesAt(Vector3 stateWorldPosition, bool isServer, out PushPull firstPushable, GameObject except = null)
	{
		firstPushable = null;
		var pushables = MatrixManager.GetAt<PushPull>(stateWorldPosition.CutToInt(), isServer);
		if (pushables.Count == 0)
		{
			return false;
		}

		for (var i = 0; i < pushables.Count; i++)
		{
			var pushable = pushables[i];
			if (pushable.gameObject == this.gameObject || except != null && pushable.gameObject == except)
			{
				continue;
			}
			firstPushable = pushable;
			return true;
		}

		return false;
	}

	#endregion

	#region Hiding/Unhiding

	[Server]
	public void DisappearFromWorldServer()
	{
		OnPullInterrupt().Invoke();
		serverState = PlayerState.HiddenState;
		serverLerpState = PlayerState.HiddenState;
		NotifyPlayers();
	}

	[Server]
	public void AppearAtPositionServer(Vector3 worldPos)
	{
		SetPosition(worldPos);
	}

	#endregion

	#region swapping positions

	/// <summary>
	/// Checks if a swap would occur due to moving to a position with a player with help intent while
	/// we have help intent and are not dragging something.
	/// If so, performs the swap by shifting the other player - this method doesn't affect our own movement/position,
	/// only the swapee is affected.
	/// </summary>
	/// <param name="targetWorldPos">target position being moved to to check for a swap at</param>
	/// <param name="inDirection">direction to which the swapee should be moved if swap occurs (should
	/// be opposite the direction of this player's movement)</param>
	/// <returns>true iff swap was performed</returns>
	private bool CheckAndDoSwap(Vector3Int targetWorldPos, Vector2 inDirection, bool isServer)
	{
		PlayerMove other = MatrixManager.GetHelpIntentAt(targetWorldPos, gameObject, isServer);
		if (other != null)
		{
			// on server, must verify that position matches
			if ((isServer && !other.PlayerScript.PlayerSync.IsMovingServer)
			    || (!isServer && !other.PlayerScript.PlayerSync.IsMovingClient))
			{
				//they've stopped there, so let's swap them
				InitiateSwap(other, targetWorldPos + inDirection.RoundToInt());
				return true;
			}
		}

		return false;
	}


	/// <summary>
	/// Invoked when someone is swapping positions with us due to arriving on our space when we have help intent.
	///
	/// Invoked on client for client prediction, server for server-authoritative logic.
	///
	/// This player is the swapee, the person displacing us is the swapper.
	/// </summary>
	/// <param name="toWorldPosition">destination to move to</param>
	/// <param name="swapper">pushpull of the person initiating the swap, to check if we should break our
	/// current pull</param>
	private void BeSwapped(Vector3Int toWorldPosition, PushPull swapper)
	{
		if (isServer)
		{
			Logger.LogFormat("Swap {0} from {1} to {2}", Category.Lerp, name, (Vector2)serverState.WorldPosition, toWorldPosition.To2Int());
			PlayerState nextStateServer = NextStateSwap(serverState, toWorldPosition, true);
			serverState = nextStateServer;
			if (pushPull != null && pushPull.IsBeingPulled && !pushPull.PulledBy == swapper)
			{
				pushPull.StopFollowing();
			}
		}
		PlayerState nextPredictedState = NextStateSwap(predictedState, toWorldPosition, false);
		//must set this on both client and server so server shows the lerp instantly as well as the client
		predictedState = nextPredictedState;
	}

	/// <summary>
	/// Called on clientside for prediction and server side for server-authoritative logic.
	///
	/// Swap the other player, sending them in direction (which should be opposite our direction of motion).
	///
	/// Doesn't affect this player's movement/position, only the swapee is affected.
	///
	/// This player is the swapper, the one they are displacing is the swapee
	/// </summary>
	/// <param name="swapee">player we will swap</param>
	/// <param name="toWorldPosition">destination to swap to</param>
	private void InitiateSwap(PlayerMove swapee, Vector3Int toWorldPosition)
	{
		swapee.PlayerScript.PlayerSync.BeSwapped(toWorldPosition, pushPull);
	}

	#endregion

	private void Start()
	{
		playerState.WorldPosition = transform.localPosition;
		//Init pending actions queue for your local player
		if (isLocalPlayer)
		{
			setLocalPlayer();
		}
		//Init pending actions queue for server
		if (isServer)
		{
			serverPendingActions = new Queue<PlayerAction>();
		}
		playerScript = GetComponent<PlayerScript>();
		registerTile = GetComponent<RegisterTile>();
		pushPull = GetComponent<PushPull>();
		playerDirectional = GetComponent<Directional>();
	}

	/// <summary>
	/// Sets up the action queue for the local player.
	/// </summary>
	public void setLocalPlayer()
	{
		pendingActions = new Queue<PlayerAction>();
		UpdatePredictedState();
		predictedSpeedClient = UIManager.WalkRun.running ? playerMove.RunSpeed : playerMove.WalkSpeed;
	}

	private void Update()
	{
		if (isLocalPlayer && playerMove != null)
		{
			//				 If being pulled by another player and you try to break free
			if (pushPull != null && pushPull.IsBeingPulledClient)
			{
				if ( !playerScript.canNotInteract() && KeyboardInputManager.IsMovementPressed() )
				{
					pushPull.CmdStopFollowing();
				}
			}
			else if (ClientPositionReady)
			{
				DoAction();
			}
		}

		Synchronize();
	}
	private void Synchronize()
	{
		if (isLocalPlayer && GameData.IsHeadlessServer)
		{
			return;
		}

		if (Matrix != null)
		{
			CheckMovementClient();
			bool server = isServer;
			if (server)
			{
				CheckMovementServer();
			}
			if (!ClientPositionReady)
			{
				Lerp();
			}
			if (server)
			{
				if (CommonInput.GetKeyDown(KeyCode.F7) && gameObject == PlayerManager.LocalPlayer)
				{
					SpawnHandler.SpawnDummyPlayer(JobType.ASSISTANT);
				}
				if (serverState.Position != serverLerpState.Position)
				{
					ServerLerp();
				}
				else
				{
					TryUpdateServerTarget();
				}

			}
		}

		//Registering
		if (registerTile.LocalPositionClient != Vector3Int.RoundToInt(predictedState.Position))
		{
			registerTile.UpdatePositionClient();
		}
		if (registerTile.LocalPositionServer != Vector3Int.RoundToInt(serverState.Position))
		{
			registerTile.UpdatePositionServer();
		}
	}

	/// <summary>
	/// Transition to next state for a swap, modifying parent matrix if matrix change occurs but not
	/// incrementing the movenumber.
	/// </summary>
	/// <param name="state">current state</param>
	/// <param name="toWorldPosition">world position new state should be in</param>
	/// <returns>state with worldposition as its worldposition, changing the parent matrix if a matrix change occurs</returns>
	private PlayerState NextStateSwap(PlayerState state, Vector3Int toWorldPosition, bool isServer)
	{
		var newState = state;
		newState.WorldPosition = toWorldPosition;

		MatrixInfo matrixAtPoint = MatrixManager.AtPoint(toWorldPosition, isServer);

		//Switching matrix while keeping world pos
		newState.MatrixId = matrixAtPoint.Id;
		newState.WorldPosition = toWorldPosition;

		return newState;
	}

	private PlayerState NextState(PlayerState state, PlayerAction action, bool isServer, bool isReplay = false)
	{
		var newState = state;
		newState.MoveNumber++;
		newState.Position = playerMove.GetNextPosition(Vector3Int.RoundToInt(state.Position), action, isReplay, MatrixManager.Get(newState.MatrixId).Matrix);

		var proposedWorldPos = newState.WorldPosition;

		MatrixInfo matrixAtPoint = MatrixManager.AtPoint(Vector3Int.RoundToInt(proposedWorldPos), isServer);

		//Switching matrix while keeping world pos
		newState.MatrixId = matrixAtPoint.Id;
		newState.WorldPosition = proposedWorldPos;

		return newState;
	}

	public void ProcessAction(PlayerAction action)
	{
		CmdProcessAction(action);
	}

#if UNITY_EDITOR
	//Visual debug
	[NonSerialized]
	private readonly Vector3 size1 = Vector3.one,
							 size2 = new Vector3(0.9f, 0.9f, 0.9f),
							 size3 = new Vector3(0.8f, 0.8f, 0.8f),
							 size4 = new Vector3(0.7f, 0.7f, 0.7f),
							 size5 = new Vector3(1.1f, 1.1f, 1.1f),
							 size6 = new Vector3(0.6f, 0.6f, 0.6f);

	[NonSerialized] private readonly Color color0 = DebugTools.HexToColor( "5566ff55" ), //blue
		color1 = Color.red,
		color2 = DebugTools.HexToColor( "fd7c6e" ), //pink
		color3 = DebugTools.HexToColor( "22e600" ), //green
		color4 = DebugTools.HexToColor( "ebfceb" ), //white
		color6 = DebugTools.HexToColor( "666666" ), //grey
		color7 = DebugTools.HexToColor( "ff666655" );//reddish
	private static readonly bool drawMoves = true;

	private void OnDrawGizmos()
	{
		//registerTile S pos
		Gizmos.color = color7;
		Vector3 regPosS = registerTile.WorldPositionServer;
		Gizmos.DrawCube(regPosS, size5);

		//registerTile C pos
		Gizmos.color = color0;
		Vector3 regPosC = registerTile.WorldPositionClient;
		Gizmos.DrawCube(regPosC, size2);

		//serverState
		Gizmos.color = color1;
		Vector3 stsPos = serverState.WorldPosition;
		Gizmos.DrawWireCube(stsPos, size1);
		DebugGizmoUtils.DrawArrow(stsPos + Vector3.left / 2, serverState.Impulse);
		if (drawMoves) DebugGizmoUtils.DrawText(serverState.MoveNumber.ToString(), stsPos + Vector3.left / 4, 15);

		//serverLerpState
		Gizmos.color = color2;
		Vector3 ssPos = serverLerpState.WorldPosition;
		Gizmos.DrawWireCube(ssPos, size2);
		DebugGizmoUtils.DrawArrow(ssPos + Vector3.right / 2, serverLerpState.Impulse);
		if (drawMoves) DebugGizmoUtils.DrawText(serverLerpState.MoveNumber.ToString(), ssPos + Vector3.right / 4, 15);

		//client predictedState
		Gizmos.color = color3;
		Vector3 clientPrediction = predictedState.WorldPosition;
		Gizmos.DrawWireCube(clientPrediction, size3);
		DebugGizmoUtils.DrawArrow(clientPrediction + Vector3.left / 5, predictedState.Impulse);
		if (drawMoves) DebugGizmoUtils.DrawText(predictedState.MoveNumber.ToString(), clientPrediction + Vector3.left, 15);

		//client playerState
		Gizmos.color = color4;
		Vector3 clientState = playerState.WorldPosition;
		Gizmos.DrawWireCube(clientState, size4);
		DebugGizmoUtils.DrawArrow(clientState + Vector3.right / 5, playerState.Impulse);
		if (drawMoves) DebugGizmoUtils.DrawText(playerState.MoveNumber.ToString(), clientState + Vector3.right, 15);

		//help intent
		Gizmos.color = isLocalPlayer ?  color4 : color1;
		if (playerMove.IsHelpIntent)
		{
			DebugGizmoUtils.DrawText("Help", clientState + Vector3.up/2, 15);
		}
	}
#endif
}
