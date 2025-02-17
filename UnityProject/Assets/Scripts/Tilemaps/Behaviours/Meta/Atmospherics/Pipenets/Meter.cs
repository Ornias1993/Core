﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

[RequireComponent(typeof(Pickupable))]
public class Meter : NBHandApplyInteractable
{
	private Pipe pipe;
	public bool anchored;
	private RegisterTile registerTile;
	private ObjectBehaviour objectBehaviour;

	public SpriteRenderer spriteRenderer;
	public List<Sprite> spriteList = new List<Sprite>();
	[SyncVar(hook = nameof(SyncSprite))] public int spriteSync;

	private void Awake() {
		registerTile = GetComponent<RegisterTile>();
		objectBehaviour = GetComponent<ObjectBehaviour>();
	}

	public override void OnStartClient()
	{
		base.OnStartClient();
		SyncSprite(spriteSync);
	}

	public void UpdateMe()
	{
		float pressure = pipe.pipenet.gasMix.Pressure;
		if(pressure >= 0)
		{
			spriteSync = 1;
		}
		else if (pressure > 500)
		{
			spriteSync = 2;
		}
		else if (pressure > 1000)
		{
			spriteSync = 3;
		}
		else if (pressure > 2000)
		{
			spriteSync = 4;
		}
	}

	protected override bool WillInteract(HandApply interaction, NetworkSide side)
	{
		if (!base.WillInteract(interaction, side))
			return false;
		if (!Validations.IsTool(interaction.HandObject, ToolType.Wrench))
			return false;
		return true;
	}

	protected override void ServerPerformInteraction(HandApply interaction)
	{
		if(anchored)
		{
			SoundManager.PlayNetworkedAtPos("Wrench", registerTile.WorldPositionServer, 1f);
			Detach();
		}
		else
		{
			var foundPipes = MatrixManager.GetAt<Pipe>(registerTile.WorldPositionServer, true);
			for (int i = 0; i < foundPipes.Count; i++)
			{
				Pipe foundPipe = foundPipes[i];
				if(foundPipe.anchored)
				{
					SoundManager.PlayNetworkedAtPos("Wrench", registerTile.WorldPositionServer, 1f);
					pipe = foundPipe;
					ToggleAnchored(true);
					UpdateManager.Instance.Add(UpdateMe);
					UpdateMe();
					break;
				}
			}
		}
	}

	public void Detach()
	{
		ToggleAnchored(false);
		spriteSync = 0;
		UpdateManager.Instance.Remove(UpdateMe);
	}

	void ToggleAnchored(bool value)
	{
		objectBehaviour.isNotPushable = value;
		anchored = value;
	}

	public void SyncSprite(int value)
	{
		spriteRenderer.sprite = spriteList[value];
	}

}