﻿using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using FairyGUI.Utils;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FairyGUI
{
	/// <summary>
	/// A UI Package contains a description file and some texture,sound assets.
	/// </summary>
	public class UIPackage
	{
		/// <summary>
		/// Package id. It is generated by the Editor, or set by customId.
		/// </summary>
		public string id { get; private set; }

		/// <summary>
		/// Package name.
		/// </summary>
		public string name { get; private set; }

		/// <summary>
		/// The path relative to the resources folder.
		/// </summary>
		public string assetPath { get; private set; }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="name"></param>
		/// <param name="extension"></param>
		/// <param name="type"></param>
		/// <returns></returns>
		public delegate object LoadResource(string name, string extension, System.Type type, out DestroyMethod destroyMethod);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="result"></param>
		public delegate void CreateObjectCallback(GObject result);

		List<PackageItem> _items;
		Dictionary<string, PackageItem> _itemsById;
		Dictionary<string, PackageItem> _itemsByName;
		Dictionary<string, string>[] _dependencies;
		AssetBundle _resBundle;
		string _customId;
		bool _fromBundle;
		LoadResource _loadFunc;

		class AtlasSprite
		{
			public PackageItem atlas;
			public Rect rect = new Rect();
			public bool rotated;
		}
		Dictionary<string, AtlasSprite> _sprites;

		static Dictionary<string, UIPackage> _packageInstById = new Dictionary<string, UIPackage>();
		static Dictionary<string, UIPackage> _packageInstByName = new Dictionary<string, UIPackage>();
		static List<UIPackage> _packageList = new List<UIPackage>();

		internal static int _constructing;

		public const string URL_PREFIX = "ui://";

		public UIPackage()
		{
			_items = new List<PackageItem>();
			_itemsById = new Dictionary<string, PackageItem>();
			_itemsByName = new Dictionary<string, PackageItem>();
			_sprites = new Dictionary<string, AtlasSprite>();
		}

		/// <summary>
		/// Return a UIPackage with a certain id.
		/// </summary>
		/// <param name="id">ID of the package.</param>
		/// <returns>UIPackage</returns>
		public static UIPackage GetById(string id)
		{
			UIPackage pkg;
			if (_packageInstById.TryGetValue(id, out pkg))
				return pkg;
			else
				return null;
		}

		/// <summary>
		/// Return a UIPackage with a certain name.
		/// </summary>
		/// <param name="name">Name of the package.</param>
		/// <returns>UIPackage</returns>
		public static UIPackage GetByName(string name)
		{
			UIPackage pkg;
			if (_packageInstByName.TryGetValue(name, out pkg))
				return pkg;
			else
				return null;
		}

		/// <summary>
		/// Add a UI package from assetbundle.
		/// </summary>
		/// <param name="bundle">A assetbundle.</param>
		/// <returns>UIPackage</returns>
		public static UIPackage AddPackage(AssetBundle bundle)
		{
			return AddPackage(bundle, bundle, null);
		}

		/// <summary>
		/// Add a UI package from two assetbundles. desc and res can be same.
		/// </summary>
		/// <param name="desc">A assetbunble contains description file.</param>
		/// <param name="res">A assetbundle contains resources.</param>
		/// <returns>UIPackage</returns>
		public static UIPackage AddPackage(AssetBundle desc, AssetBundle res)
		{
			return AddPackage(desc, res, null);
		}

		/// <summary>
		/// Add a UI package from two assetbundles with a optional main asset name.
		/// </summary>
		/// <param name="desc">A assetbunble contains description file.</param>
		/// <param name="res">A assetbundle contains resources.</param>
		/// <param name="mainAssetName">Main asset name. e.g. Basics_fui.bytes</param>
		/// <returns>UIPackage</returns>
		public static UIPackage AddPackage(AssetBundle desc, AssetBundle res, string mainAssetName)
		{
			byte[] source = null;
#if (UNITY_5 || UNITY_5_3_OR_NEWER)
			if (mainAssetName != null)
			{
				TextAsset ta = desc.LoadAsset<TextAsset>(mainAssetName);
				if (ta != null)
					source = ta.bytes;
			}
			else
			{
				string[] names = desc.GetAllAssetNames();
				string searchPattern = "_fui";
				foreach (string n in names)
				{
					if (n.IndexOf(searchPattern) != -1)
					{
						TextAsset ta = desc.LoadAsset<TextAsset>(n);
						if (ta != null)
						{
							source = ta.bytes;
							mainAssetName = Path.GetFileNameWithoutExtension(n);
							break;
						}
					}
				}
			}
#else
			if (mainAssetName != null)
			{
				TextAsset ta = (TextAsset)desc.Load(mainAssetName, typeof(TextAsset));
				if (ta != null)
					source = ta.bytes;
			}
			else
			{
				source = ((TextAsset)desc.mainAsset).bytes;
				mainAssetName = desc.mainAsset.name;
			}
#endif
			if (source == null)
				throw new Exception("FairyGUI: no package found in this bundle.");

			if (desc != res)
				desc.Unload(true);

			ByteBuffer buffer = new ByteBuffer(source);

			UIPackage pkg = new UIPackage();
			pkg._resBundle = res;
			pkg._fromBundle = true;
			int pos = mainAssetName.IndexOf("_fui");
			string assetNamePrefix;
			if (pos != -1)
				assetNamePrefix = mainAssetName.Substring(0, pos);
			else
				assetNamePrefix = mainAssetName;
			if (!pkg.LoadPackage(buffer, res.name, assetNamePrefix))
				return null;

			_packageInstById[pkg.id] = pkg;
			_packageInstByName[pkg.name] = pkg;
			_packageList.Add(pkg);

			return pkg;
		}

		/// <summary>
		/// Add a UI package from a path relative to Unity Resources path.
		/// </summary>
		/// <param name="descFilePath">Path relative to Unity Resources path.</param>
		/// <returns>UIPackage</returns>
		public static UIPackage AddPackage(string descFilePath)
		{
			if (descFilePath.StartsWith("Assets/"))
			{
#if UNITY_EDITOR
				return AddPackage(descFilePath, (string name, string extension, System.Type type, out DestroyMethod destroyMethod) =>
				{
					destroyMethod = DestroyMethod.Unload;
					return AssetDatabase.LoadAssetAtPath(name + extension, type);
				});
#else
				
				Debug.LogWarning("FairyGUI: failed to load package in '" + descFilePath + "'");
				return null;
#endif
			}
			return AddPackage(descFilePath, (string name, string extension, System.Type type, out DestroyMethod destroyMethod) =>
			{
				destroyMethod = DestroyMethod.Unload;
				return Resources.Load(name, type);
			});
		}

		/// <summary>
		/// 使用自定义的加载方式载入一个包。
		/// </summary>
		/// <param name="assetPath">包资源路径。</param>
		/// <param name="loadFunc">载入函数</param>
		/// <returns></returns>
		public static UIPackage AddPackage(string assetPath, LoadResource loadFunc)
		{
			if (_packageInstById.ContainsKey(assetPath))
				return _packageInstById[assetPath];

			DestroyMethod dm;
			TextAsset asset = (TextAsset)loadFunc(assetPath + "_fui", ".bytes", typeof(TextAsset), out dm);
			if (asset == null)
			{
				if (Application.isPlaying)
					throw new Exception("FairyGUI: Cannot load ui package in '" + assetPath + "'");
				else
					Debug.LogWarning("FairyGUI: Cannot load ui package in '" + assetPath + "'");
			}

			ByteBuffer buffer = new ByteBuffer(asset.bytes);

			UIPackage pkg = new UIPackage();
			pkg._loadFunc = loadFunc;
			pkg.assetPath = assetPath;
			if (!pkg.LoadPackage(buffer, assetPath, assetPath))
				return null;

			_packageInstById[pkg.id] = pkg;
			_packageInstByName[pkg.name] = pkg;
			_packageInstById[assetPath] = pkg;
			_packageList.Add(pkg);
			return pkg;
		}

		/// <summary>
		/// 使用自定义的加载方式载入一个包。
		/// </summary>
		/// <param name="descData">描述文件数据。</param>
		/// <param name="assetNamePrefix">资源文件名前缀。如果包含，则载入资源时名称将传入assetNamePrefix@resFileName这样格式。可以为空。</param>
		/// <param name="loadFunc">载入函数</param>
		/// <returns></returns>
		public static UIPackage AddPackage(byte[] descData, string assetNamePrefix, LoadResource loadFunc)
		{
			ByteBuffer buffer = new ByteBuffer(descData);

			UIPackage pkg = new UIPackage();
			pkg._loadFunc = loadFunc;
			if (!pkg.LoadPackage(buffer, "raw data", assetNamePrefix))
				return null;

			_packageInstById[pkg.id] = pkg;
			_packageInstByName[pkg.name] = pkg;
			_packageList.Add(pkg);

			return pkg;
		}

		/// <summary>
		/// Remove a package. All resources in this package will be disposed.
		/// </summary>
		/// <param name="packageIdOrName"></param>
		/// <param name="allowDestroyingAssets"></param>
		public static void RemovePackage(string packageIdOrName)
		{
			UIPackage pkg = null;
			if (!_packageInstById.TryGetValue(packageIdOrName, out pkg))
			{
				if (!_packageInstByName.TryGetValue(packageIdOrName, out pkg))
					throw new Exception("FairyGUI: '" + packageIdOrName + "' is not a valid package id or name.");
			}
			pkg.Dispose();
			_packageInstById.Remove(pkg.id);
			if (pkg._customId != null)
				_packageInstById.Remove(pkg._customId);
			if (pkg.assetPath != null)
				_packageInstById.Remove(pkg.assetPath);
			_packageInstByName.Remove(pkg.name);
			_packageList.Remove(pkg);
		}

		/// <summary>
		/// 
		/// </summary>
		public static void RemoveAllPackages()
		{
			if (_packageInstById.Count > 0)
			{
				UIPackage[] pkgs = _packageList.ToArray();

				foreach (UIPackage pkg in pkgs)
				{
					pkg.Dispose();
				}
			}
			_packageList.Clear();
			_packageInstById.Clear();
			_packageInstByName.Clear();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public static List<UIPackage> GetPackages()
		{
			return _packageList;
		}

		/// <summary>
		/// Create a UI object.
		/// </summary>
		/// <param name="pkgName">Package name.</param>
		/// <param name="resName">Resource name.</param>
		/// <returns>A UI object.</returns>
		public static GObject CreateObject(string pkgName, string resName)
		{
			UIPackage pkg = GetByName(pkgName);
			if (pkg != null)
				return pkg.CreateObject(resName);
			else
				return null;
		}

		/// <summary>
		///  Create a UI object.
		/// </summary>
		/// <param name="pkgName">Package name.</param>
		/// <param name="resName">Resource name.</param>
		/// <param name="userClass">Custom implementation of this object.</param>
		/// <returns>A UI object.</returns>
		public static GObject CreateObject(string pkgName, string resName, System.Type userClass)
		{
			UIPackage pkg = GetByName(pkgName);
			if (pkg != null)
				return pkg.CreateObject(resName, userClass);
			else
				return null;
		}

		/// <summary>
		/// Create a UI object.
		/// </summary>
		/// <param name="url">Resource url.</param>
		/// <returns>A UI object.</returns>
		public static GObject CreateObjectFromURL(string url)
		{
			PackageItem pi = GetItemByURL(url);
			if (pi != null)
				return pi.owner.CreateObject(pi, null);
			else
				return null;
		}

		/// <summary>
		/// Create a UI object.
		/// </summary>
		/// <param name="url">Resource url.</param>
		/// <param name="userClass">Custom implementation of this object.</param>
		/// <returns>A UI object.</returns>
		public static GObject CreateObjectFromURL(string url, System.Type userClass)
		{
			PackageItem pi = GetItemByURL(url);
			if (pi != null)
				return pi.owner.CreateObject(pi, userClass);
			else
				return null;
		}

		public static void CreateObjectAsync(string pkgName, string resName, CreateObjectCallback callback)
		{
			UIPackage pkg = GetByName(pkgName);
			if (pkg != null)
				pkg.CreateObjectAsync(resName, callback);
			else
				Debug.LogError("FairyGUI: package not found - " + pkgName);
		}

		public static void CreateObjectFromURL(string url, CreateObjectCallback callback)
		{
			PackageItem pi = GetItemByURL(url);
			if (pi != null)
				AsyncCreationHelper.CreateObject(pi, callback);
			else
				Debug.LogError("FairyGUI: resource not found - " + url);
		}

		/// <summary>
		/// Get a asset with a certain name.
		/// </summary>
		/// <param name="pkgName">Package name.</param>
		/// <param name="resName">Resource name.</param>
		/// <returns>If resource is atlas, returns NTexture; If resource is sound, returns AudioClip.</returns>
		public static object GetItemAsset(string pkgName, string resName)
		{
			UIPackage pkg = GetByName(pkgName);
			if (pkg != null)
				return pkg.GetItemAsset(resName);
			else
				return null;
		}

		/// <summary>
		/// Get a asset with a certain name.
		/// </summary>
		/// <param name="url">Resource url.</param>
		/// <returns>If resource is atlas, returns NTexture; If resource is sound, returns AudioClip.</returns>
		public static object GetItemAssetByURL(string url)
		{
			PackageItem item = GetItemByURL(url);
			if (item == null)
				return null;

			return item.owner.GetItemAsset(item);
		}

		/// <summary>
		/// Get url of an item in package.
		/// </summary>
		/// <param name="pkgName">Package name.</param>
		/// <param name="resName">Resource name.</param>
		/// <returns>Url.</returns>
		public static string GetItemURL(string pkgName, string resName)
		{
			UIPackage pkg = GetByName(pkgName);
			if (pkg == null)
				return null;

			PackageItem pi;
			if (!pkg._itemsByName.TryGetValue(resName, out pi))
				return null;

			return URL_PREFIX + pkg.id + pi.id;
		}

		public static PackageItem GetItemByURL(string url)
		{
			if (url == null)
				return null;

			int pos1 = url.IndexOf("//");
			if (pos1 == -1)
				return null;

			int pos2 = url.IndexOf('/', pos1 + 2);
			if (pos2 == -1)
			{
				if (url.Length > 13)
				{
					string pkgId = url.Substring(5, 8);
					UIPackage pkg = GetById(pkgId);
					if (pkg != null)
					{
						string srcId = url.Substring(13);
						return pkg.GetItem(srcId);
					}
				}
			}
			else
			{
				string pkgName = url.Substring(pos1 + 2, pos2 - pos1 - 2);
				UIPackage pkg = GetByName(pkgName);
				if (pkg != null)
				{
					string srcName = url.Substring(pos2 + 1);
					return pkg.GetItemByName(srcName);
				}
			}

			return null;
		}

		/// <summary>
		/// 将'ui://包名/组件名'转换为以内部id表达的url格式。如果传入的url本身就是内部id格式，则直接返回。
		/// 同时这个方法还带格式检测，如果传入不正确的url，会返回null。
		/// </summary>
		/// <param name="url"></param>
		/// <returns></returns>
		public static string NormalizeURL(string url)
		{
			if (url == null)
				return null;

			int pos1 = url.IndexOf("//");
			if (pos1 == -1)
				return null;

			int pos2 = url.IndexOf('/', pos1 + 2);
			if (pos2 == -1)
				return url;
			else
			{
				string pkgName = url.Substring(pos1 + 2, pos2 - pos1 - 2);
				string srcName = url.Substring(pos2 + 1);
				return GetItemURL(pkgName, srcName);
			}
		}

		/// <summary>
		/// Set strings source.
		/// </summary>
		/// <param name="source"></param>
		public static void SetStringsSource(XML source)
		{
			TranslationHelper.LoadFromXML(source);
		}

		/// <summary>
		/// Set a custom id for package, then you can use it in GetById.
		/// </summary>
		public string customId
		{
			get { return _customId; }
			set
			{
				if (_customId != null)
					_packageInstById.Remove(_customId);
				_customId = value;
				if (_customId != null)
					_packageInstById[_customId] = this;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public AssetBundle resBundle
		{
			get { return _resBundle; }
		}

		/// <summary>
		/// 获得本包依赖的包的id列表
		/// </summary>
		public Dictionary<string, string>[] dependencies
		{
			get { return _dependencies; }
		}

		bool LoadPackage(ByteBuffer buffer, string packageSource, string assetNamePrefix)
		{
			if (buffer.ReadUint() != 0x46475549)
			{
				if (Application.isPlaying)
					throw new Exception("FairyGUI: old package format found in '" + packageSource + "'");
				else
				{
					Debug.LogWarning("FairyGUI: old package format found in '" + packageSource + "'");
					return false;
				}
			}

			buffer.version = buffer.ReadInt();
			buffer.ReadBool(); //compressed
			id = buffer.ReadString();
			name = buffer.ReadString();
			if (_packageInstById.ContainsKey(id) && name != _packageInstById[id].name)
			{
				Debug.LogWarning("FairyGUI: Package id conflicts, '" + name + "' and '" + _packageInstById[id].name + "'");
				return false;
			}
			buffer.Skip(20);
			int indexTablePos = buffer.position;
			int cnt;

			buffer.Seek(indexTablePos, 4);

			cnt = buffer.ReadInt();
			string[] stringTable = new string[cnt];
			for (int i = 0; i < cnt; i++)
				stringTable[i] = buffer.ReadString();
			buffer.stringTable = stringTable;

			buffer.Seek(indexTablePos, 1);

			PackageItem pi;

			if (assetNamePrefix == null)
				assetNamePrefix = string.Empty;
			else if (assetNamePrefix.Length > 0)
				assetNamePrefix = assetNamePrefix + "_";

			cnt = buffer.ReadShort();
			for (int i = 0; i < cnt; i++)
			{
				int nextPos = buffer.ReadInt();
				nextPos += buffer.position;

				pi = new PackageItem();
				pi.owner = this;
				pi.type = (PackageItemType)buffer.ReadByte();
				pi.id = buffer.ReadS();
				pi.name = buffer.ReadS();
				buffer.ReadS(); //path
				pi.file = buffer.ReadS();
				pi.exported = buffer.ReadBool();
				pi.width = buffer.ReadInt();
				pi.height = buffer.ReadInt();

				switch (pi.type)
				{
					case PackageItemType.Image:
						{
							pi.objectType = ObjectType.Image;
							int scaleOption = buffer.ReadByte();
							if (scaleOption == 1)
							{
								Rect rect = new Rect();
								rect.x = buffer.ReadInt();
								rect.y = buffer.ReadInt();
								rect.width = buffer.ReadInt();
								rect.height = buffer.ReadInt();
								pi.scale9Grid = rect;

								pi.tileGridIndice = buffer.ReadInt();
							}
							else if (scaleOption == 2)
								pi.scaleByTile = true;

							buffer.ReadBool(); //smoothing
							break;
						}

					case PackageItemType.MovieClip:
						{
							buffer.ReadBool(); //smoothing
							pi.objectType = ObjectType.MovieClip;
							pi.rawData = buffer.ReadBuffer();
							break;
						}

					case PackageItemType.Font:
						{
							pi.rawData = buffer.ReadBuffer();
							break;
						}

					case PackageItemType.Component:
						{
							int extension = buffer.ReadByte();
							if (extension > 0)
								pi.objectType = (ObjectType)extension;
							else
								pi.objectType = ObjectType.Component;
							pi.rawData = buffer.ReadBuffer();

							UIObjectFactory.ResolvePackageItemExtension(pi);
							break;
						}

					case PackageItemType.Atlas:
					case PackageItemType.Sound:
					case PackageItemType.Misc:
						{
							pi.file = assetNamePrefix + pi.file;
							break;
						}
				}
				_items.Add(pi);
				_itemsById[pi.id] = pi;
				if (pi.name != null)
					_itemsByName[pi.name] = pi;

				buffer.position = nextPos;
			}

			buffer.Seek(indexTablePos, 2);

			cnt = buffer.ReadShort();
			for (int i = 0; i < cnt; i++)
			{
				int nextPos = buffer.ReadShort();
				nextPos += buffer.position;

				string itemId = buffer.ReadS();
				pi = _itemsById[buffer.ReadS()];

				AtlasSprite sprite = new AtlasSprite();
				sprite.atlas = pi;
				sprite.rect.x = buffer.ReadInt();
				sprite.rect.y = buffer.ReadInt();
				sprite.rect.width = buffer.ReadInt();
				sprite.rect.height = buffer.ReadInt();
				sprite.rotated = buffer.ReadBool();
				_sprites[itemId] = sprite;

				buffer.position = nextPos;
			}

			if (buffer.Seek(indexTablePos, 3))
			{
				cnt = buffer.ReadShort();
				for (int i = 0; i < cnt; i++)
				{
					int nextPos = buffer.ReadInt();
					nextPos += buffer.position;

					if (_itemsById.TryGetValue(buffer.ReadS(), out pi))
					{
						if (pi.type == PackageItemType.Image)
						{
							pi.pixelHitTestData = new PixelHitTestData();
							pi.pixelHitTestData.Load(buffer);
						}
					}

					buffer.position = nextPos;
				}
			}

			if (!Application.isPlaying)
				_items.Sort(ComparePackageItem);

			buffer.Seek(indexTablePos, 0);
			cnt = buffer.ReadShort();
			_dependencies = new Dictionary<string, string>[cnt];
			for (int i = 0; i < cnt; i++)
			{
				Dictionary<string, string> kv = new Dictionary<string, string>();
				kv.Add("id", buffer.ReadS());
				kv.Add("name", buffer.ReadS());
				_dependencies[i] = kv;
			}

			return true;
		}

		static int ComparePackageItem(PackageItem p1, PackageItem p2)
		{
			if (p1.name != null && p2.name != null)
				return p1.name.CompareTo(p2.name);
			else
				return 0;
		}

		/// <summary>
		/// 
		/// </summary>
		public void LoadAllAssets()
		{
			int cnt = _items.Count;
			for (int i = 0; i < cnt; i++)
				GetItemAsset(_items[i]);
		}

		/// <summary>
		/// 
		/// </summary>
		public void UnloadAssets()
		{
			int cnt = _items.Count;
			for (int i = 0; i < cnt; i++)
			{
				PackageItem pi = _items[i];
				if (pi.type == PackageItemType.Atlas)
				{
					if (pi.texture != null)
						pi.texture.Unload();
				}
				else if (pi.type == PackageItemType.Sound)
				{
					if (pi.audioClip != null)
						pi.audioClip.Unload();
				}
			}

			if (_resBundle != null)
			{
				_resBundle.Unload(true);
				_resBundle = null;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public void ReloadAssets()
		{
			if (_fromBundle)
				throw new Exception("FairyGUI: new bundle must be passed to this function");

			ReloadAssets(null);
		}

		/// <summary>
		/// 
		/// </summary>
		public void ReloadAssets(AssetBundle resBundle)
		{
			_resBundle = resBundle;
			_fromBundle = _resBundle != null;

			int cnt = _items.Count;
			for (int i = 0; i < cnt; i++)
			{
				PackageItem pi = _items[i];
				if (pi.type == PackageItemType.Atlas)
				{
					if (pi.texture != null && pi.texture.nativeTexture == null)
						LoadAtlas(pi);
				}
				else if (pi.type == PackageItemType.Sound)
				{
					if (pi.audioClip != null && pi.audioClip.nativeClip == null)
						LoadSound(pi);
				}
			}
		}

		void Dispose()
		{
			int cnt = _items.Count;
			for (int i = 0; i < cnt; i++)
			{
				PackageItem pi = _items[i];
				if (pi.type == PackageItemType.Atlas)
				{
					if (pi.texture != null)
					{
						pi.texture.Dispose();
						pi.texture = null;
					}
				}
				else if (pi.type == PackageItemType.Sound)
				{
					if (pi.audioClip != null)
					{
						pi.audioClip.Unload();
						pi.audioClip = null;
					}
				}
			}
			_items.Clear();

			if (_resBundle != null)
			{
				_resBundle.Unload(true);
				_resBundle = null;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="resName"></param>
		/// <returns></returns>
		public GObject CreateObject(string resName)
		{
			PackageItem pi;
			if (!_itemsByName.TryGetValue(resName, out pi))
			{
				Debug.LogError("FairyGUI: resource not found - " + resName + " in " + this.name);
				return null;
			}

			return CreateObject(pi, null);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="resName"></param>
		/// <param name="userClass"></param>
		/// <returns></returns>
		public GObject CreateObject(string resName, System.Type userClass)
		{
			PackageItem pi;
			if (!_itemsByName.TryGetValue(resName, out pi))
			{
				Debug.LogError("FairyGUI: resource not found - " + resName + " in " + this.name);
				return null;
			}

			return CreateObject(pi, userClass);
		}

		public void CreateObjectAsync(string resName, CreateObjectCallback callback)
		{
			PackageItem pi;
			if (!_itemsByName.TryGetValue(resName, out pi))
			{
				Debug.LogError("FairyGUI: resource not found - " + resName + " in " + this.name);
				return;
			}

			AsyncCreationHelper.CreateObject(pi, callback);
		}

		GObject CreateObject(PackageItem item, System.Type userClass)
		{
			Stats.LatestObjectCreation = 0;
			Stats.LatestGraphicsCreation = 0;

			GetItemAsset(item);

			GObject g = null;
			if (item.type == PackageItemType.Component)
			{
				if (userClass != null)
					g = (GComponent)Activator.CreateInstance(userClass);
				else
					g = UIObjectFactory.NewObject(item);
			}
			else
				g = UIObjectFactory.NewObject(item);

			if (g == null)
				return null;

			_constructing++;
			g.packageItem = item;
			g.ConstructFromResource();
			_constructing--;
			return g;
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="resName"></param>
		/// <returns></returns>
		public object GetItemAsset(string resName)
		{
			PackageItem pi;
			if (!_itemsByName.TryGetValue(resName, out pi))
			{
				Debug.LogError("FairyGUI: Resource not found - " + resName + " in " + this.name);
				return null;
			}

			return GetItemAsset(pi);
		}

		public List<PackageItem> GetItems()
		{
			return _items;
		}

		public PackageItem GetItem(string itemId)
		{
			PackageItem pi;
			if (_itemsById.TryGetValue(itemId, out pi))
				return pi;
			else
				return null;
		}

		public PackageItem GetItemByName(string itemName)
		{
			PackageItem pi;
			if (_itemsByName.TryGetValue(itemName, out pi))
				return pi;
			else
				return null;
		}

		public object GetItemAsset(PackageItem item)
		{
			switch (item.type)
			{
				case PackageItemType.Image:
					if (item.texture == null)
						LoadImage(item);
					return item.texture;

				case PackageItemType.Atlas:
					if (item.texture == null)
						LoadAtlas(item);
					return item.texture;

				case PackageItemType.Sound:
					if (item.audioClip == null)
						LoadSound(item);
					return item.audioClip;

				case PackageItemType.Font:
					if (item.bitmapFont == null)
						LoadFont(item);

					return item.bitmapFont;

				case PackageItemType.MovieClip:
					if (item.frames == null)
						LoadMovieClip(item);

					return item.frames;

				case PackageItemType.Component:
					return item.rawData;

				case PackageItemType.Misc:
					return LoadBinary(item);

				default:
					return null;
			}
		}

		void LoadAtlas(PackageItem item)
		{
			string ext = Path.GetExtension(item.file);
			string fileName = item.file.Substring(0, item.file.Length - ext.Length);

			Texture tex = null;
			Texture alphaTex = null;
			DestroyMethod dm;

			if (_fromBundle)
			{
				if (_resBundle != null)
				{
#if (UNITY_5 || UNITY_5_3_OR_NEWER)
					tex = _resBundle.LoadAsset<Texture>(fileName);
#else
					tex = (Texture2D)_resBundle.Load(fileName, typeof(Texture2D));
#endif
				}
				else
					Debug.LogWarning("FairyGUI: bundle already unloaded.");

				dm = DestroyMethod.None;
			}
			else
				tex = (Texture)_loadFunc(fileName, ext, typeof(Texture), out dm);

			if (tex == null)
				Debug.LogWarning("FairyGUI: texture '" + item.file + "' not found in " + this.name);
			else if (!(tex is Texture2D))
			{
				Debug.LogWarning("FairyGUI: settings for '" + item.file + "' is wrong! Correct values are: (Texture Type=Default, Texture Shape=2D)");
				tex = null;
			}
			else
			{
				if (((Texture2D)tex).mipmapCount > 1)
					Debug.LogWarning("FairyGUI: settings for '" + item.file + "' is wrong! Correct values are: (Generate Mip Maps=unchecked)");
			}

			if (tex != null)
			{
				fileName = fileName + "!a";
				if (_fromBundle)
				{
					if (_resBundle != null)
					{
#if (UNITY_5 || UNITY_5_3_OR_NEWER)
						alphaTex = _resBundle.LoadAsset<Texture2D>(fileName);
#else
						alphaTex = (Texture2D)_resBundle.Load(fileName, typeof(Texture2D));
#endif
					}
				}
				else
					alphaTex = (Texture2D)_loadFunc(fileName, ext, typeof(Texture2D), out dm);
			}


			if (tex == null)
			{
				tex = NTexture.CreateEmptyTexture();
				dm = DestroyMethod.Destroy;
			}

			if (item.texture == null)
			{
				item.texture = new NTexture(tex, alphaTex, (float)tex.width / item.width, (float)tex.height / item.height);
				item.texture.destroyMethod = dm;
			}
			else
			{
				item.texture.Reload(tex, alphaTex);
				item.texture.destroyMethod = dm;
			}
		}

		void LoadImage(PackageItem item)
		{
			AtlasSprite sprite;
			if (_sprites.TryGetValue(item.id, out sprite))
				item.texture = new NTexture((NTexture)GetItemAsset(sprite.atlas), sprite.rect, sprite.rotated);
			else
				item.texture = NTexture.Empty;
		}

		void LoadSound(PackageItem item)
		{
			string ext = Path.GetExtension(item.file);
			string fileName = item.file.Substring(0, item.file.Length - ext.Length);

			AudioClip audioClip = null;
			DestroyMethod dm;

			if (_resBundle != null)
			{
#if (UNITY_5 || UNITY_5_3_OR_NEWER)
				audioClip = _resBundle.LoadAsset<AudioClip>(fileName);
#else
				audioClip = (AudioClip)_resBundle.Load(fileName, typeof(AudioClip));
#endif
				dm = DestroyMethod.None;
			}
			else
			{
				audioClip = (AudioClip)_loadFunc(fileName, ext, typeof(AudioClip), out dm);
			}

			if (item.audioClip == null)
				item.audioClip = new NAudioClip(audioClip);
			else
				item.audioClip.Reload(audioClip);
			item.audioClip.destroyMethod = dm;
		}

		byte[] LoadBinary(PackageItem item)
		{
			string ext = Path.GetExtension(item.file);
			string fileName = item.file.Substring(0, item.file.Length - ext.Length);

			TextAsset ta;
			if (_resBundle != null)
			{
#if (UNITY_5 || UNITY_5_3_OR_NEWER)
				ta = _resBundle.LoadAsset<TextAsset>(fileName);
#else
				ta = (TextAsset)_resBundle.Load(fileName, typeof(TextAsset));
#endif
				if (ta != null)
					return ta.bytes;
				else
					return null;
			}
			else
			{
				DestroyMethod dm;
				object ret = _loadFunc(fileName, ext, typeof(TextAsset), out dm);
				if (ret == null)
					return null;
				if (ret is byte[])
					return (byte[])ret;
				else
					return ((TextAsset)ret).bytes;
			}
		}

		void LoadMovieClip(PackageItem item)
		{
			ByteBuffer buffer = item.rawData;

			buffer.Seek(0, 0);

			item.interval = buffer.ReadInt() / 1000f;
			item.swing = buffer.ReadBool();
			item.repeatDelay = buffer.ReadInt() / 1000f;

			buffer.Seek(0, 1);

			int frameCount = buffer.ReadShort();
			item.frames = new MovieClip.Frame[frameCount];

			string spriteId;
			MovieClip.Frame frame;
			AtlasSprite sprite;

			for (int i = 0; i < frameCount; i++)
			{
				int nextPos = buffer.ReadShort();
				nextPos += buffer.position;

				frame = new MovieClip.Frame();
				frame.rect.x = buffer.ReadInt();
				frame.rect.y = buffer.ReadInt();
				frame.rect.width = buffer.ReadInt();
				frame.rect.height = buffer.ReadInt();
				frame.addDelay = buffer.ReadInt() / 1000f;
				spriteId = buffer.ReadS();

				if (spriteId != null && _sprites.TryGetValue(spriteId, out sprite))
				{
					if (item.texture == null)
						item.texture = (NTexture)GetItemAsset(sprite.atlas);
					frame.uvRect = new Rect(sprite.rect.x / item.texture.width * item.texture.uvRect.width,
						1 - sprite.rect.yMax * item.texture.uvRect.height / item.texture.height,
						sprite.rect.width * item.texture.uvRect.width / item.texture.width,
						sprite.rect.height * item.texture.uvRect.height / item.texture.height);
					frame.rotated = sprite.rotated;
					if (frame.rotated)
					{
						float tmp = frame.uvRect.width;
						frame.uvRect.width = frame.uvRect.height;
						frame.uvRect.height = tmp;
					}
				}
				item.frames[i] = frame;

				buffer.position = nextPos;
			}
		}

		void LoadFont(PackageItem item)
		{
			BitmapFont font = new BitmapFont(item);
			item.bitmapFont = font;
			ByteBuffer buffer = item.rawData;

			buffer.Seek(0, 0);

			bool ttf = buffer.ReadBool();
			font.canTint = buffer.ReadBool();
			font.resizable = buffer.ReadBool();
			font.hasChannel = buffer.ReadBool();
			int fontSize = buffer.ReadInt();
			int xadvance = buffer.ReadInt();
			int lineHeight = buffer.ReadInt();

			float texScaleX = 1;
			float texScaleY = 1;
			NTexture mainTexture = null;
			AtlasSprite mainSprite = null;
			if (ttf && _sprites.TryGetValue(item.id, out mainSprite))
			{
				mainTexture = (NTexture)GetItemAsset(mainSprite.atlas);
				texScaleX = mainTexture.root.uvRect.width / mainTexture.width;
				texScaleY = mainTexture.root.uvRect.height / mainTexture.height;
			}

			buffer.Seek(0, 1);

			BitmapFont.BMGlyph bg;
			int cnt = buffer.ReadInt();
			for (int i = 0; i < cnt; i++)
			{
				int nextPos = buffer.ReadShort();
				nextPos += buffer.position;

				bg = new BitmapFont.BMGlyph();
				char ch = buffer.ReadChar();
				font.AddChar(ch, bg);

				string img = buffer.ReadS();
				int bx = buffer.ReadInt();
				int by = buffer.ReadInt();
				bg.offsetX = buffer.ReadInt();
				bg.offsetY = buffer.ReadInt();
				bg.width = buffer.ReadInt();
				bg.height = buffer.ReadInt();
				bg.advance = buffer.ReadInt();
				bg.channel = buffer.ReadByte();
				if (bg.channel == 1)
					bg.channel = 3;
				else if (bg.channel == 2)
					bg.channel = 2;
				else if (bg.channel == 3)
					bg.channel = 1;

				if (ttf)
				{
					if (mainSprite.rotated)
					{
						bg.uv[0] = new Vector2((float)(by + bg.height + mainSprite.rect.x) * texScaleX,
							1 - (float)(mainSprite.rect.yMax - bx) * texScaleY);
						bg.uv[1] = new Vector2(bg.uv[0].x - (float)bg.height * texScaleX, bg.uv[0].y);
						bg.uv[2] = new Vector2(bg.uv[1].x, bg.uv[0].y + (float)bg.width * texScaleY);
						bg.uv[3] = new Vector2(bg.uv[0].x, bg.uv[2].y);
					}
					else
					{
						bg.uv[0] = new Vector2((float)(bx + mainSprite.rect.x) * texScaleX,
							1 - (float)(by + bg.height + mainSprite.rect.y) * texScaleY);
						bg.uv[1] = new Vector2(bg.uv[0].x, bg.uv[0].y + (float)bg.height * texScaleY);
						bg.uv[2] = new Vector2(bg.uv[0].x + (float)bg.width * texScaleX, bg.uv[1].y);
						bg.uv[3] = new Vector2(bg.uv[2].x, bg.uv[0].y);
					}

					bg.lineHeight = lineHeight;
				}
				else
				{
					PackageItem charImg;
					if (_itemsById.TryGetValue(img, out charImg))
					{
						GetItemAsset(charImg);
						Rect uvRect = charImg.texture.uvRect;
						bg.uv[0] = uvRect.position;
						bg.uv[1] = new Vector2(uvRect.xMin, uvRect.yMax);
						bg.uv[2] = new Vector2(uvRect.xMax, uvRect.yMax);
						bg.uv[3] = new Vector2(uvRect.xMax, uvRect.yMin);
						if (charImg.texture.rotated)
							NGraphics.RotateUV(bg.uv, ref uvRect);
						bg.width = charImg.texture.width;
						bg.height = charImg.texture.height;

						if (mainTexture == null)
							mainTexture = charImg.texture.root;
					}

					if (fontSize == 0)
						fontSize = bg.height;

					if (bg.advance == 0)
					{
						if (xadvance == 0)
							bg.advance = bg.offsetX + bg.width;
						else
							bg.advance = xadvance;
					}

					bg.lineHeight = bg.offsetY < 0 ? bg.height : (bg.offsetY + bg.height);
					if (bg.lineHeight < font.size)
						bg.lineHeight = font.size;
				}

				buffer.position = nextPos;
			}

			font.size = fontSize;
			font.mainTexture = mainTexture;
			if (!font.hasChannel)
				font.shader = ShaderConfig.imageShader;
		}
	}
}
