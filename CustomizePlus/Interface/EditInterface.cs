﻿// © Customize+.
// Licensed under the MIT license.

namespace CustomizePlus.Interface
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Numerics;
	using System.Windows.Forms;
	using Anamnesis.Files;
	using Anamnesis.Posing;
	using CustomizePlus.Memory;
	using Dalamud.Interface;
	using Dalamud.Interface.Components;
	using Dalamud.Logging;
	using ImGuiNET;
	using Newtonsoft.Json;
	using static CustomizePlus.BodyScale;

	public class EditInterface : WindowBase
	{
		protected BodyScale? Scale { get; private set; }

		protected override string Title => $"(WIP) Edit Scale: {this.originalScaleName}";
		protected BodyScale? ScaleUpdated { get; private set; }

		private int scaleIndex = -1;

		private string newScaleName = string.Empty;
		private string newScaleCharacter = string.Empty;
		private string originalScaleName = string.Empty;
		private string originalScaleCharacter = string.Empty;
		private HkVector4 originalScaleValue = HkVector4.One;
		private Vector4 newScaleValue = HkVector4.One.GetAsNumericsVector();
		private Vector4 originalRootScale = new Vector4(1f, 1f, 1f, 0f);
		private Vector4 newRootScale = HkVector4.One.GetAsNumericsVector();

		private BodyScale? scaleStart;
		private Dictionary<string, HkVector4>? boneValuesOriginal = new Dictionary<string, HkVector4>();
		private Dictionary<string, HkVector4>? boneValuesNew = new Dictionary<string, HkVector4>();
		private readonly List<string> boneNamesLegacy = LegacyBoneNameConverter.GetLegacyNames();
		private readonly List<string> boneNamesModern = LegacyBoneNameConverter.GetModernNames();
		private List<string> boneNamesModernUsed = new List<string>();
		private List<string> boneNamesLegacyUsed = new List<string>();
		private bool scaleEnabled = false;
		private bool reset = false;

		public void Show(BodyScale scale)
		{
			Configuration config = Plugin.Configuration;
			EditInterface editWnd = Plugin.InterfaceManager.Show<EditInterface>();
			editWnd.Scale = scale;
			editWnd.ScaleUpdated = scale;
			if (scale == null)
			{
				scale = new BodyScale();
			}

			editWnd.scaleStart = scale;
			editWnd.ScaleUpdated = scale;
			editWnd.originalScaleName = scale.ScaleName;
			editWnd.originalScaleCharacter = scale.CharacterName;
			editWnd.newScaleCharacter = scale.CharacterName;

			editWnd.scaleEnabled = scale.BodyScaleEnabled;

			for (int i = 0; i < editWnd.boneNamesLegacy.Count && i < editWnd.boneNamesModern.Count; i++)
			{
				HkVector4 tempBone = HkVector4.One;
				if (scale.Bones.TryGetValue(editWnd.boneNamesLegacy[i], out tempBone))
				{
					editWnd.boneValuesOriginal.Add(editWnd.boneNamesLegacy[i], tempBone);
					editWnd.boneValuesNew.Add(editWnd.boneNamesLegacy[i], tempBone);
					editWnd.boneNamesModernUsed.Add(editWnd.boneNamesModern[i]);
					editWnd.boneNamesLegacyUsed.Add(editWnd.boneNamesLegacy[i]);
				}
			}

			editWnd.originalRootScale = scale.RootScale.GetAsNumericsVector();

			editWnd.newRootScale = editWnd.originalRootScale;

			editWnd.originalScaleName = scale.ScaleName;
			editWnd.originalScaleCharacter = scale.CharacterName;
			editWnd.newScaleName = editWnd.originalScaleName;
			editWnd.newScaleCharacter = editWnd.originalScaleCharacter;

			editWnd.scaleIndex = -1;
		}

		protected override void DrawContents()
		{
			Configuration config = Plugin.Configuration;

			string newScaleNameTemp = this.newScaleName;
			string newScaleCharacterTemp = this.newScaleCharacter;
			bool enabledTemp = this.scaleEnabled;
			bool resetTemp = this.reset;

			if (ImGui.Checkbox("Enable", ref enabledTemp))
			{
				this.scaleEnabled = enabledTemp;
				if (config.AutomaticEditMode)
				{
					AddToConfig(this.newScaleName, this.newScaleCharacter);
					config.Save();
					Plugin.LoadConfig();
				}
			}

			ImGui.SameLine();

			ImGui.SetNextItemWidth(200);

			if (ImGui.InputText("Character Name", ref newScaleCharacterTemp, 1024))
			{
				this.newScaleCharacter = newScaleCharacterTemp;
			}

			ImGui.SameLine();

			ImGui.SetNextItemWidth(300);
			if (ImGui.InputText("Scale Name", ref newScaleNameTemp, 1024))
			{
				this.newScaleName = newScaleNameTemp;
			}

			ImGui.SameLine();

			bool autoModeEnable = config.AutomaticEditMode;
			if (ImGui.Checkbox("Automatic Mode", ref autoModeEnable))
			{
				config.AutomaticEditMode = autoModeEnable;
			}

			if (ImGui.IsItemHovered())
				ImGui.SetTooltip($"Applies changes automatically without saving.");

			ImGui.Separator();

			Vector4 rootScaleLocal = this.newRootScale;

			if (ImGuiComponents.IconButton(-1, FontAwesomeIcon.Recycle))
			{
				rootScaleLocal = new Vector4(1f, 1f, 1f, 1f);
				this.newRootScale = rootScaleLocal;
				if (config.AutomaticEditMode)
				{
					this.UpdateCurrent("Root", new HkVector4(1f, 1f, 1f, 1f));
				}
				this.reset = true;
			}

			if (ImGui.IsItemHovered())
				ImGui.SetTooltip($"Reset");

			ImGui.SameLine();

			Vector4 rootScaleLocalTemp = new Vector4((float)rootScaleLocal.X, (float)rootScaleLocal.Y, (float)rootScaleLocal.Z, (float)rootScaleLocal.W);

			if (ImGui.DragFloat4("Root", ref rootScaleLocalTemp, 0.001f, 0f, 10f))
			{
				if (this.reset)
				{
					rootScaleLocalTemp = new Vector4(1f, 1f, 1f, 1f);
					this.reset = false;
				}
				else if (!((rootScaleLocalTemp.X == rootScaleLocalTemp.Y) && (rootScaleLocalTemp.X == rootScaleLocalTemp.Z) && (rootScaleLocalTemp.Y == rootScaleLocalTemp.Z)))
				{
					rootScaleLocalTemp.W = 0;
				}
				else if (rootScaleLocalTemp.W != 0)
				{
					rootScaleLocalTemp.X = rootScaleLocalTemp.W;
					rootScaleLocalTemp.Y = rootScaleLocalTemp.W;
					rootScaleLocalTemp.Z = rootScaleLocalTemp.W;
				}
				rootScaleLocal = new Vector4(rootScaleLocalTemp.X, rootScaleLocalTemp.Y, rootScaleLocalTemp.Z, rootScaleLocalTemp.W);
				this.newRootScale = rootScaleLocal;
				if (config.AutomaticEditMode)
				{
					this.UpdateCurrent("Root", new HkVector4(rootScaleLocal.X, rootScaleLocal.Y, rootScaleLocal.Z, rootScaleLocalTemp.W));
				}
			}

			ImGui.Separator();
			ImGui.BeginTable("Bones", 6, ImGuiTableFlags.SizingStretchSame);
			ImGui.TableNextColumn();
			ImGui.Text("Bones:");
			ImGui.TableNextColumn();
			ImGui.Text("X");
			ImGui.TableNextColumn();
			ImGui.Text("Y");
			ImGui.TableNextColumn();
			ImGui.Text("Z");
			ImGui.TableNextColumn();
			ImGui.Text("All");
			ImGui.TableNextColumn();
			ImGui.Text("Name");
			ImGui.EndTable();

			ImGui.BeginChild("scrolling", new Vector2(0, ImGui.GetFrameHeightWithSpacing() - 56), false);

			for (int i = 0; i < boneValuesNew.Count; i++)
			{
				string boneNameLocalLegacy = this.boneNamesLegacyUsed[i];

				string boneNameLocalModern = this.boneNamesModernUsed[i];

				ImGui.PushID(i);

				if (!this.IsBoneNameEditable(boneNameLocalModern))
				{
					ImGui.PopID();
					continue;
				}

				HkVector4 currentHkVector = HkVector4.One;
				string label = "Not Found";

				try
				{
					if (this.boneValuesNew.TryGetValue(boneNameLocalLegacy, out currentHkVector))
					{
						label = boneNameLocalModern;
					}
					else if (this.boneValuesNew.TryGetValue(boneNameLocalModern, out currentHkVector))
					{
						label = boneNameLocalModern;
					}
					else
					{
						currentHkVector = HkVector4.One;
					}
				}
				catch (Exception ex)
				{

				}

				Vector4 currentVector4 = currentHkVector.GetAsNumericsVector();

				if (ImGuiComponents.IconButton(i, FontAwesomeIcon.Recycle))
				{
					this.reset = true;
				}

				if (ImGui.IsItemHovered())
					ImGui.SetTooltip($"Reset");

				if (this.reset)
				{
					currentVector4.W = 1F;
					currentVector4.X = 1F;
					currentVector4.Y = 1F;
					currentVector4.Z = 1F;
					this.reset = false;
					try
					{
						if (this.boneValuesNew.ContainsKey(boneNameLocalModern))
						{
							this.boneValuesNew[boneNameLocalModern] = new HkVector4(currentVector4.X, currentVector4.Y, currentVector4.Z, currentVector4.W);
						}
						else if (this.boneValuesNew.Remove(boneNameLocalLegacy))
						{
							this.boneValuesNew[boneNameLocalLegacy] = new HkVector4(currentVector4.X, currentVector4.Y, currentVector4.Z, currentVector4.W);
						}
						else
						{
							throw new Exception();
						}
					}
					catch
					{
						//throw new Exception();
					}
					if (config.AutomaticEditMode)
					{
						this.UpdateCurrent(boneNameLocalLegacy, new HkVector4(currentVector4.X, currentVector4.Y, currentVector4.Z, currentVector4.W));
					}
				}
				else if (currentVector4.X == currentVector4.Y && currentVector4.Y == currentVector4.Z)
				{
					currentVector4.W = currentVector4.X;
				}
				else
				{
					currentVector4.W = 0;
				}

				ImGui.SameLine();

				ImGui.SetNextItemWidth(ImGui.GetWindowWidth() - 190);
				if (ImGui.DragFloat4(label, ref currentVector4, 0.001f, 0f, 10f))
				{
					try
					{
						if (this.reset)
						{
							currentVector4.W = 1F;
							currentVector4.X = 1F;
							currentVector4.Y = 1F;
							currentVector4.Z = 1F;
							this.reset = false;
						}
						else if (!((currentVector4.X == currentVector4.Y) && (currentVector4.X == currentVector4.Z) && (currentVector4.Y == currentVector4.Z)))
						{
							currentVector4.W = 0;
						}
						else if (currentVector4.W != 0)
						{
							currentVector4.X = currentVector4.W;
							currentVector4.Y = currentVector4.W;
							currentVector4.Z = currentVector4.W;
						}
					}
					catch (Exception ex)
					{

					}
					try
					{
						if (this.boneValuesNew.ContainsKey(boneNameLocalModern))
						{
							this.boneValuesNew[boneNameLocalModern] = new HkVector4(currentVector4.X, currentVector4.Y, currentVector4.Z, currentVector4.W);
						}
						else if (this.boneValuesNew.Remove(boneNameLocalLegacy))
						{
							this.boneValuesNew[boneNameLocalLegacy] = new HkVector4(currentVector4.X, currentVector4.Y, currentVector4.Z, currentVector4.W);
						}
						else
						{
							throw new Exception();
						}
					}
					catch
					{
						//throw new Exception();
					}
					if (config.AutomaticEditMode)
					{
						this.UpdateCurrent(boneNameLocalLegacy, new HkVector4(currentVector4.X, currentVector4.Y, currentVector4.Z, currentVector4.W));
					}
				}


				ImGui.PopID();
			}

			ImGui.EndChild();

			ImGui.Separator();

			if (ImGui.Button("Save"))
			{
				AddToConfig(this.newScaleName, this.newScaleCharacter);
				if (this.newScaleCharacter != this.originalScaleCharacter)
					this.originalScaleCharacter = this.newScaleCharacter;
				if (this.newScaleName != this.originalScaleName)
					this.originalScaleName = this.newScaleName;
				config.Save();
				Plugin.LoadConfig();
			}

			/* TODO feature: undo of some variety. Option below is a revert to what was present when edit was opened, but needs additonal logic
			 * ImGui.SameLine();
			if (ImGui.Button("Revert"))
			{
				RevertToOriginal();
				//config.Save();
			}
			*/

			ImGui.SameLine();

			if (ImGui.Button("Save and Close"))
			{
				AddToConfig(this.newScaleName, this.newScaleCharacter);
				config.Save();
				Plugin.LoadConfig();
				this.Close();
			}
			ImGui.SameLine();
			if (ImGui.Button("Cancel"))
			{
				this.Close();
			}

			ImGui.SameLine();

			ImGui.Text("    Save and close with new scale name or new character name to create a copy.");
		}

		private void AddToConfig(string scaleName, string characterName)
		{
			Configuration config = Plugin.Configuration;
			BodyScale newBody = new BodyScale();

			for (int i = 0; i < this.boneNamesLegacy.Count && i < this.boneValuesNew.Count; i++)
			{
				string legacyName = boneNamesLegacyUsed[i];

				if (!this.ScaleUpdated.Bones.ContainsKey(legacyName))
					newBody.Bones.Add(legacyName, this.boneValuesNew[legacyName]);

				newBody.Bones[legacyName] = this.boneValuesNew[legacyName];

				newBody.BodyScaleEnabled = this.scaleEnabled;
				newBody.ScaleName = scaleName;
				newBody.CharacterName = characterName;
			}

			newBody.RootScale = new HkVector4(this.newRootScale.X, this.newRootScale.Y, this.newRootScale.Z, 0);
			if (this.originalScaleName == scaleName && this.originalScaleCharacter == characterName)
			{
				int matchIndex = -1;
				for (int i = 0; i < config.BodyScales.Count; i++)
				{
					if (config.BodyScales[i].ScaleName == scaleName && config.BodyScales[i].CharacterName == characterName)
					{
						matchIndex = i;
						break;
					}
				}
				if (matchIndex >= 0)
				{
					config.BodyScales.RemoveAt(matchIndex);
					config.BodyScales.Insert(matchIndex, newBody);
				}
			}
			else
			{
				this.originalScaleName = scaleName;
				this.originalScaleCharacter = characterName;
				config.BodyScales.Add(newBody);
				if (this.scaleEnabled)
				{
					config.ToggleOffAllOtherMatching(characterName, scaleName);
				}
			}
		}

		private void RevertToOriginal() //Currently Unused
		{
			this.boneValuesNew = this.boneValuesOriginal;
			this.newRootScale = this.originalRootScale;
		}

		private void UpdateCurrent(string boneName, HkVector4 boneValue)
		{
			Configuration config = Plugin.Configuration;
			BodyScale newBody = this.ScaleUpdated;

			if (boneName == "Root")
			{
				newBody.RootScale = boneValue;
			}
			else
			{
				newBody.Bones[boneName] = boneValue;
			}

			if (this.scaleIndex == -1 || this.scaleIndex > config.BodyScales.Count)
			{
				this.scaleIndex = GetCurrentScaleIndex(this.originalScaleName, this.originalScaleCharacter);
			}

			config.BodyScales[this.scaleIndex] = newBody;
			config.Save();
			Plugin.LoadConfig();
		}

		private int GetCurrentScaleIndex(string scaleName, string scaleCharacter)
		{
			Configuration config = Plugin.Configuration;
			int matchIndex = -1;
			for (int i = 0; i < config.BodyScales.Count; i++)
			{
				if (config.BodyScales[i].ScaleName == scaleName && config.BodyScales[i].CharacterName == scaleCharacter)
				{
					matchIndex = i;
					break;
				}
			}
			if (matchIndex >= 0)
			{
				return matchIndex;
			}
			return -1;
		}

		private bool IsBoneNameEditable(string boneNameModern)
		{
			// Megahack method
			if (boneNameModern == "Root" || boneNameModern == "Throw" || boneNameModern == "Abdomen" 
				|| boneNameModern.Contains("Cloth") || boneNameModern.Contains("Scabbard")
				|| boneNameModern.Contains("Holster") || boneNameModern.Contains("Poleyn") || boneNameModern.Contains("Shield")
				|| boneNameModern.Contains("Couter") || boneNameModern.Contains("Weapon") || boneNameModern.Contains("Sheathe"))
				return false;
			return true;
		}
	}
}
