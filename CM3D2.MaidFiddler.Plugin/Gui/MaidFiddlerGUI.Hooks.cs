﻿using System.Collections.Generic;
using System.Drawing;
using System.IO;
using CM3D2.MaidFiddler.Hook;
using CM3D2.MaidFiddler.Plugin.Utils;
using param;
using Schedule;

namespace CM3D2.MaidFiddler.Plugin.Gui
{
    public partial class MaidFiddlerGUI
    {
        private void InitHookCallbacks()
        {
            MaidStatusChangeHooks.StatusChanged += OnStatusChanged;
            MaidStatusChangeHooks.ThumbnailChanged += OnMaidThumbnailChanged;
            MaidStatusChangeHooks.StatusChangedID += OnStatusChanged;
            MaidStatusChangeHooks.ClassUpdated += OnClassUpdated;
            MaidStatusChangeHooks.NewProperty += OnPropertyHasChanged;
            MaidStatusChangeHooks.PropertyRemoved += OnPropertyHasChanged;
            MaidStatusChangeHooks.CheckWorkEnabled += OnWorkEnabledCheck;
            MaidStatusChangeHooks.ProcessNoonWorkData += ReloadNoonWorkData;
            MaidStatusChangeHooks.ProcessNightWorkData += ReloadNightWorkData;
            MaidStatusChangeHooks.StatusUpdated += OnStatusUpdated;
            MaidStatusChangeHooks.FeaturePropensityUpdated += OnFeaturePropensityUpdated;
            MaidStatusChangeHooks.CommandUpdate += OnCommandUpdate;
            MaidStatusChangeHooks.NightWorkVisibilityCheck += OnNightWorkVisibilityCheck;
            MaidStatusChangeHooks.YotogiSkillVisibilityCheck += OnYotogiSkillVisibilityCheck;
            MaidStatusChangeHooks.YotogiSkillVisibilityStageCheck += OnYotogiSkillVisibilityStageCheck;
            MaidStatusChangeHooks.ProcessFreeModeScene += ProcessFreeModeScene;

            PlayerStatusChangeHooks.PlayerValueChanged += OnPlayerValueChanged;

            ValueLimitHooks.ToggleValueLimit += OnValueRound;
        }

        private void ProcessFreeModeScene(object sender, PostProcessSceneEventArgs e)
        {
            if (forceAllScenesEnabled)
            {
                Debugger.WriteLine("Forcing free mode scene visible");
                e.ForceEnabled = true;
            }
            else
                Debugger.WriteLine("Processing free mode scene");
        }

        private void OnClassUpdated(object sender, HookEventArgs args)
        {
            Debugger.WriteLine("Updating maid and/or yotogi class info.");
            MaidInfo maid = SelectedMaid;
            if (maid == null)
            {
                Debugger.WriteLine(LogLevel.Warning, "Maid is NULL!");
                return;
            }

            if (maid.Maid != args.CallerMaid)
            {
                Debugger.WriteLine(LogLevel.Warning, "Caller maid is not the selected one! Aborting...");
                return;
            }

            if (valueUpdateQueue[currentQueue].ContainsKey(args.Tag))
            {
                Debugger.WriteLine(LogLevel.Error, "Tag already in update queue! Aborting...");
                return;
            }
            switch (args.Tag)
            {
                case MaidChangeType.MaidClassType:
                    valueUpdateQueue[currentQueue].Add(args.Tag, () => maid.UpdateMaidClasses());
                    break;
                case MaidChangeType.YotogiClassType:
                    valueUpdateQueue[currentQueue].Add(args.Tag, () => maid.UpdateYotogiClasses());
                    break;
                case MaidChangeType.MaidAndYotogiClass:
                    valueUpdateQueue[currentQueue].Add(
                    args.Tag,
                    () =>
                    {
                        maid.UpdateMaidBonusValues();
                        maid.UpdateMaidClasses();
                        maid.UpdateYotogiClasses();
                    });
                    break;
            }
        }

        private void OnCommandUpdate(object sender, CommandUpdateEventArgs args)
        {
            if (!allYotogiCommandsVisible)
                return;
            Debugger.Assert(
            () =>
            {
                for (int i = 0; i < args.Commands[args.PlayerState].Length; i++)
                {
                    Yotogi.SkillData.Command.Data data = args.Commands[args.PlayerState][i];
                    args.CommandFactory.AddCommand(data);
                }
            },
            "Failed to manually add yotogi command");
        }

        private void OnFeaturePropensityUpdated(object sender, UpdateFeaturePropensityEventArgs args)
        {
            Debugger.Assert(
            () =>
            {
                MaidInfo maid = SelectedMaid;
                if (maid == null)
                    return;

                if (args.CallerMaid != maid.Maid)
                    return;

                if (args.UpdateFeature)
                {
                    Debugger.WriteLine(LogLevel.Info, "Updating all features!");
                    for (Feature e = Feature.Null + 1; e < EnumHelper.MaxFeature; e++)
                        maid.UpdateMiscStatus(MaidChangeType.Feature, (int) e);
                }
                else if (args.UpdatePropensity)
                {
                    Debugger.WriteLine(LogLevel.Info, "Updating all propensities!");
                    for (Propensity e = Propensity.Null + 1; e < EnumHelper.MaxPropensity; e++)
                        maid.UpdateMiscStatus(MaidChangeType.Propensity, (int) e);
                }
            },
            "Failed to update maid features/propensities");
        }

        private void OnMaidThumbnailChanged(object sender, ThumbnailEventArgs args)
        {
            if (!IsMaidLoaded(args.Maid))
                return;

            Image img;
            using (MemoryStream stream = new MemoryStream(args.Maid.GetThumIcon().EncodeToPNG()))
            {
                img = Image.FromStream(stream);
            }

            if (!maidThumbnails.ContainsKey(args.Maid.Param.status.guid))
                maidThumbnails.Add(args.Maid.Param.status.guid, img);
            else
            {
                maidThumbnails[args.Maid.Param.status.guid].Dispose();
                maidThumbnails.Remove(args.Maid.Param.status.guid);
                maidThumbnails.Add(args.Maid.Param.status.guid, img);
            }

            listBox1.Invalidate();
        }

        private void OnNightWorkVisibilityCheck(object sender, NightWorkVisibleEventArgs e)
        {
            Debugger.WriteLine("Attempting to check for visibility");
            ScheduleCSVData.NightWorkType workType = ScheduleCSVData.NightWorkData[e.WorkID].nightWorkType;
            if (workType == ScheduleCSVData.NightWorkType.Trainee || workType == ScheduleCSVData.NightWorkType.Trainer)
                return;
            if (!vipAlwaysVisible)
                return;
            e.Force = true;
            e.Visible = true;
            Debugger.WriteLine("Visibility set to true!");
        }

        private void OnPlayerValueChanged(object sender, PlayerValueChangeEventArgs args)
        {
            if (Player.Player == null)
            {
                Debugger.WriteLine(LogLevel.Warning, "Player is NULL! Aborting!");
                return;
            }

            Debugger.WriteLine(LogLevel.Info, $"Attempting to update value {args.Tag}");
            if (Player.IsLocked(args.Tag))
            {
                Debugger.WriteLine(LogLevel.Info, "Value locked! Aborting changes...");
                args.Block = true;
                return;
            }

            if (!playerValueUpdateQueue.ContainsKey(args.Tag))
                playerValueUpdateQueue.Add(args.Tag, () => Player.UpdateField(args.Tag));
        }

        private void OnPropertyHasChanged(object sender, StatusChangedEventArgs args)
        {
            MaidInfo maid = SelectedMaid;
            if (maid == null)
                return;

            if (maid.Maid != args.CallerMaid)
                return;

            if (valueUpdateQueue[currentQueue].ContainsKey(args.Tag))
            {
                Debugger.WriteLine(LogLevel.Warning, $"Tag already in update queue {currentQueue}! Aborting...");
                return;
            }
            switch (args.Tag)
            {
                case MaidChangeType.NewGetWork:
                case MaidChangeType.Work:
                    valueUpdateQueue[currentQueue].Add(args.Tag, () => maid.UpdateHasWork(args.ID));
                    break;
                case MaidChangeType.NewGetSkill:
                case MaidChangeType.Skill:
                    valueUpdateQueue[currentQueue].Add(args.Tag, () => maid.UpdateHasSkill(args.ID));
                    break;
            }
        }

        private void OnStatusChanged(object sender, StatusChangedEventArgs args)
        {
            Debugger.WriteLine($"Changed status for property {EnumHelper.GetName(args.Tag)}");
            MaidInfo maid = SelectedMaid;
            if (maid == null)
            {
                Debugger.WriteLine(LogLevel.Warning, "Maid is NULL!");
                return;
            }

            if (maid.Maid != args.CallerMaid)
            {
                Debugger.WriteLine(LogLevel.Warning, "Caller maid is not the selected one! Aborting...");
                return;
            }

            if (!valueUpdateQueue[currentQueue].ContainsKey(args.Tag))
                valueUpdateQueue[currentQueue].Add(args.Tag, () => maid.UpdateField(args.Tag, args.ID, args.Value));
            else
                Debugger.WriteLine(LogLevel.Warning, $"Tag already in update queue {currentQueue}! Aborting...");
        }

        private void OnStatusChanged(object sender, StatusEventArgs args)
        {
            Debugger.WriteLine($"Changed status for property {EnumHelper.GetName(args.Tag)}");
            Debugger.WriteLine(
            $"Called maid: {args.CallerMaid.Param.status.first_name} {args.CallerMaid.Param.status.last_name}");

            if (!IsMaidLoaded(args.CallerMaid))
            {
                Debugger.WriteLine(LogLevel.Error, "Maid not in the list! Aborting!");
                return;
            }

            MaidInfo maid = GetMaidInfo(args.CallerMaid);

            if (maid.IsLocked(args.Tag))
            {
                Debugger.WriteLine(LogLevel.Info, "Value locked! Aborting changes...");
                args.BlockAssignment = true;
                return;
            }

            if (SelectedMaid != null && SelectedMaid.Maid != maid.Maid)
            {
                Debugger.WriteLine(LogLevel.Warning, "Selected maids are different!");
                return;
            }

            if (!valueUpdateQueue[currentQueue].ContainsKey(args.Tag))
            {
                Debugger.WriteLine(LogLevel.Info, "Adding to update queue");
                valueUpdateQueue[currentQueue].Add(args.Tag, () => maid.UpdateField(args.Tag));
            }
            else
            {
                Debugger.WriteLine(
                LogLevel.Warning,
                $"Already in update queue {currentQueue}! Queue length: {valueUpdateQueue[currentQueue].Count}");
            }
        }

        private void OnStatusUpdated(object sender, StatusUpdateEventArgs args)
        {
            MaidInfo maid = SelectedMaid;
            if (maid == null)
                return;

            if (args.CallerMaid != maid.Maid)
                return;

            Debugger.WriteLine(
            LogLevel.Info,
            $"Updating {EnumHelper.GetName(args.Tag)}.{(args.Tag == MaidChangeType.Feature ? Translation.GetTranslation(EnumHelper.GetName((Feature) args.EnumVal)) : Translation.GetTranslation(EnumHelper.GetName((Propensity) args.EnumVal)))} to {args.Value}...");

            maid.UpdateMiscStatus(args.Tag, args.EnumVal, args.Value);
        }

        private void OnValueRound(object sender, ValueLimitEventArgs args)
        {
            args.RemoveLimit = removeValueLimit;
        }

        private void OnWorkEnabledCheck(object sender, WorkEventArgs args)
        {
            if (args.CallerMaid == null)
                return;
            if (!IsMaidLoaded(args.CallerMaid))
                return;

            switch (args.Tag)
            {
                case MaidChangeType.NoonWorkId:
                    args.ForceEnabled = GetMaidInfo(args.CallerMaid).IsNoonWorkForceEnabled(args.ID);
                    break;
                case MaidChangeType.NightWorkId:
                    args.ForceEnabled = GetMaidInfo(args.CallerMaid).IsNightWorkForceEnabled(args.ID);
                    break;
            }
            Debugger.WriteLine(
            LogLevel.Info,
            $"Attempting to check work enabled: ID={args.ID}, Force={args.ForceEnabled}");
        }

        private void OnYotogiSkillVisibilityStageCheck(object sender, YotogiSkillVisibleEventArgs e)
        {
            Debugger.WriteLine(
            LogLevel.Info,
            $"Checking for skill stage visibility. Forcing visible: {yotogiAllSkillsVisible}");
            e.ForceVisible = yotogiAllSkillsVisible;
        }

        private void OnYotogiSkillVisibilityCheck(object sender, YotogiSkillVisibleEventArgs e)
        {
            Debugger.WriteLine(
            LogLevel.Info,
            $"Checking for skill visibility. Forcing visible: {yotogiSkillsVisible || yotogiAllSkillsVisible}");
            e.ForceVisible = yotogiSkillsVisible || yotogiAllSkillsVisible;
        }

        private void ReloadNightWorkData(object sender, PostProcessNightEventArgs args)
        {
            Maid m = args.ScheduleScene.slot[args.SlotID].maid;
            if (m == null || !IsMaidLoaded(m))
                return;

            Debugger.WriteLine("Reloading all night works...");
            UpdateNightWorksData(args.ScheduleScene.slot[args.SlotID]);
        }

        private void ReloadNoonWorkData(object sender, PostProcessNoonEventArgs args)
        {
            Maid m = args.ScheduleScene.slot[args.SlotID].maid;
            if (m == null || !IsMaidLoaded(m))
                return;

            Debugger.WriteLine("Reloading all noon works...");
            UpdateNoonWorkData(args.ScheduleScene.slot[args.SlotID]);
        }

        private void RemoveHookCallbacks()
        {
            MaidStatusChangeHooks.StatusChanged -= OnStatusChanged;
            MaidStatusChangeHooks.ThumbnailChanged -= OnMaidThumbnailChanged;
            MaidStatusChangeHooks.StatusChangedID -= OnStatusChanged;
            MaidStatusChangeHooks.ClassUpdated -= OnClassUpdated;
            MaidStatusChangeHooks.NewProperty -= OnPropertyHasChanged;
            MaidStatusChangeHooks.PropertyRemoved -= OnPropertyHasChanged;
            MaidStatusChangeHooks.CheckWorkEnabled -= OnWorkEnabledCheck;
            MaidStatusChangeHooks.ProcessNoonWorkData -= ReloadNoonWorkData;
            MaidStatusChangeHooks.ProcessNightWorkData -= ReloadNightWorkData;
            MaidStatusChangeHooks.StatusUpdated -= OnStatusUpdated;
            MaidStatusChangeHooks.FeaturePropensityUpdated -= OnFeaturePropensityUpdated;
            MaidStatusChangeHooks.CommandUpdate -= OnCommandUpdate;
            MaidStatusChangeHooks.NightWorkVisibilityCheck -= OnNightWorkVisibilityCheck;
            MaidStatusChangeHooks.YotogiSkillVisibilityCheck -= OnYotogiSkillVisibilityCheck;
            MaidStatusChangeHooks.ProcessFreeModeScene -= ProcessFreeModeScene;

            PlayerStatusChangeHooks.PlayerValueChanged -= OnPlayerValueChanged;

            ValueLimitHooks.ToggleValueLimit -= OnValueRound;
        }

        private void UpdateNightWorksData(Slot slot)
        {
            slot.nightWorksData.Clear();
            foreach (KeyValuePair<int, ScheduleCSVData.NightWork> work in ScheduleCSVData.NightWorkData)
            {
                slot.nightWorksData.Add(new NightWork(slot, work.Key));
            }
        }

        private void UpdateNoonWorkData(Slot slot)
        {
            slot.noonWorksData.Clear();
            foreach (KeyValuePair<int, ScheduleCSVData.NoonWork> work in ScheduleCSVData.NoonWorkData)
            {
                slot.noonWorksData.Add(new NoonWork(slot, work.Key));
            }
        }
    }
}