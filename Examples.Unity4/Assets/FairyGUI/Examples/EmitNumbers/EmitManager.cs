﻿using System.Collections.Generic;
using UnityEngine;
using FairyGUI;

public class EmitManager
{
	static EmitManager _instance;
	public static EmitManager inst
	{
		get
		{
			if (_instance == null)
				_instance = new EmitManager();
			return _instance;
		}
	}

	public string hurtFont1;
	public string hurtFont2;
	public string criticalSign;

	public GComponent view { get; private set; }
	public Camera mainCamera { get; private set; }

	private readonly Stack<EmitComponent> _componentPool = new Stack<EmitComponent>();

	public EmitManager()
	{
		hurtFont1 = UIPackage.GetItemURL("EmitNumbers", "number1");
		hurtFont2 = UIPackage.GetItemURL("EmitNumbers", "number2");
		criticalSign = UIPackage.GetItemURL("EmitNumbers", "critical");

		view = new GComponent();
		GRoot.inst.AddChild(view);

		mainCamera = GameObject.Find("Main Camera").GetComponent<Camera>();
	}

	public void Emit(Transform owner, int type, long hurt, bool critical)
	{
		EmitComponent ec;
		if (_componentPool.Count > 0)
			ec = _componentPool.Pop();
		else
			ec = new EmitComponent();
		ec.SetHurt(owner, type, hurt, critical);
	}

	public void ReturnComponent(EmitComponent com)
	{
		_componentPool.Push(com);
	}
}