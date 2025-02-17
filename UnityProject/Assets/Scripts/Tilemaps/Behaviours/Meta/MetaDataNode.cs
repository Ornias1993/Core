﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Atmospherics;
using UnityEngine;

/// <summary>
/// Holds all of the metadata associated with an individual tile, such as for atmospherics simulation, damage.
/// </summary>
public class MetaDataNode: IGasMixContainer
{
	public static readonly MetaDataNode None;

	/// <summary>
	/// Local position of this tile in its parent matrix.
	/// </summary>
	public readonly Vector3Int Position;

	/// <summary>
	/// If this node is in a closed room, it's assigned to it by the room's number
	/// </summary>
	public int RoomNumber = -1;

	/// <summary>
	/// Type of this node.
	/// </summary>
	public NodeType Type;

	/// <summary>
	/// The mixture of gases currently on this node.
	/// </summary>
	public GasMix GasMix { get; set; }

	/// <summary>
	/// The hotspot state of this node - indicates a potential to ignite gases, and
	/// ignites them if conditions are met. Null if no potential exists on this tile.
	/// </summary>
	public Hotspot Hotspot;

	/// <summary>
	/// Current damage inflicted on this tile.
	/// </summary>
	public float Damage;

	public Vector2Int 	WindDirection 	= Vector2Int.zero;
	public float		WindForce 		= 0;

	/// <summary>
	/// Number of neighboring MetaDataNodes
	/// </summary>
	public int NeighborCount
	{
		get
		{
			lock (neighbors)
			{
				return neighbors.Count;
			}
		}
	}

	/// <summary>
	/// Current drying coroutine.
	/// </summary>
	public IEnumerator CurrentDrying;

	/// <summary>
	/// Whether tile is already scorched
	/// </summary>
	public bool IsScorched;

	/// <summary>
	/// The current neighbor nodes. Nodes can be Null!
	/// </summary>
	public readonly MetaDataNode[] Neighbors = new MetaDataNode[4];

	private List<MetaDataNode> neighbors;

	public ReactionManager ReactionManager => reactionManager;
	private ReactionManager reactionManager;


	/// <summary>
	/// Create a new MetaDataNode on the specified local position (within the parent matrix)
	/// </summary>
	/// <param name="position">local position (within the matrix) the node exists on</param>
	public MetaDataNode(Vector3Int position, ReactionManager reactionManager)
	{
		Position = position;
		neighbors = new List<MetaDataNode>();
		GasMix = GasMixes.Space;
		this.reactionManager = reactionManager;
	}

	static MetaDataNode()
	{
		None = new MetaDataNode(Vector3Int.one * -1000000, null);
	}

	/// <summary>
	/// Is this tile in space
	/// </summary>
	public bool IsSpace => Type == NodeType.Space;

	/// <summary>
	/// Is this tile in a room
	/// </summary>
	public bool IsRoom => Type == NodeType.Room;

	/// <summary>
	/// Does this tile contain a closed airlock/shutters?
	/// (used for gas freezing)
	/// </summary>
	public bool IsClosedAirlock { get; set; }

	/// <summary>
	/// Is this tile occupied by something impassable (airtight!)
	/// </summary>
	public bool IsOccupied => Type == NodeType.Occupied;

	public bool IsSlippery = false;

	public bool Exists => this != None;

	public void AddNeighborsToList(ref List<MetaDataNode> list)
	{
		lock (neighbors)
		{
			foreach (MetaDataNode neighbor in neighbors)
			{
				list.Add(neighbor);
			}
		}
	}

	public void ClearNeighbors()
	{
		lock (neighbors)
		{
			neighbors.Clear();
		}
	}

	public void AddNeighbor(MetaDataNode neighbor)
	{
		if (neighbor != this)
		{
			lock (neighbors)
			{
				neighbors.Add(neighbor);

				SyncNeighbors();
			}
		}
	}

	public bool HasHotspot => Hotspot != null;
	public bool HasWind => WindDirection != Vector2Int.zero;

	public void RemoveNeighbor(MetaDataNode neighbor)
	{
		lock (neighbors)
		{
			neighbors.Remove(neighbor);

			SyncNeighbors();
		}
	}

	public string WindowDmgType { get; set; } = "";


	public void ResetDamage()
	{
		Damage = 0;
	}

	public override string ToString()
	{
		return Position.ToString();
	}

	private void SyncNeighbors()
	{
		for (int i = 0; i < Neighbors.Length; i++)
		{
			Neighbors[i] = i < neighbors.Count ? neighbors[i] : null;
		}
	}
}