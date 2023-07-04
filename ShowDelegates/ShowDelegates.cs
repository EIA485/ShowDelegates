using HarmonyLib;
using NeosModLoader;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using FrooxEngine;
using FrooxEngine.UIX;
using BaseX;

namespace ShowDelegates
{
    public class ShowDelegates : NeosMod
    {
        public override string Name => "ShowDelegates";
        public override string Author => "art0007i";
        public override string Version => "1.1.1";
        public override string Link => "https://github.com/art0007i/ShowDelegates/";

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> KEY_DEFAULT_OPEN = new ModConfigurationKey<bool>("default_open", "If true delegates will be expanded by default", () => false);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> KEY_SHOW_DELEGATES = new ModConfigurationKey<bool>("show_deleages", "If false delegates will not be shown", () => true);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> KEY_SHOW_HIDDEN = new ModConfigurationKey<bool>("show_hidden", "If false items with the hidden HideInInspector attribute will not be shown", () => true);
        private static ModConfiguration config;
        private static MethodInfo DelegateProxyMethod = typeof(ShowDelegates).GetMethod(nameof(GenerateDelegateProxy), BindingFlags.NonPublic | BindingFlags.Static);
        public override void OnEngineInit()
        {
            Harmony harmony = new Harmony("me.art0007i.ShowDelegates");
            harmony.PatchAll();
            config = GetConfiguration();
        }
        private static void GenerateDelegateProxy<T>(UIBuilder ui, string name, T target) where T : class
        {
            LocaleString localeString = name + ":";
            Text text = ui.Text(localeString, true, new Alignment?(Alignment.MiddleLeft), true, null);
            text.Slot.GetComponent<RectTransform>(null, false).AnchorMax.Value = new float2(0.25f, 1f);
            InteractionElement.ColorDriver colorDriver = text.Slot.AttachComponent<Button>(true, null).ColorDrivers.Add();
            colorDriver.ColorDrive.Target = text.Color;
            colorDriver.NormalColor.Value = color.Black;
            colorDriver.HighlightColor.Value = color.Blue;
            colorDriver.PressColor.Value = color.Blue;
            text.Slot.AttachComponent<DelegateProxySource<T>>(true, null).Delegate.Target = target;
        }
        private static void GenerateGenericDelegateProxy(UIBuilder ui, string name, Delegate target, Type type) => DelegateProxyMethod.MakeGenericMethod(type).Invoke(null, new object[] { ui, name, target });
        private static void GenerateReferenceProxy(UIBuilder ui, string name, IWorldElement target)
        {
            LocaleString localeString = name + ":";
            Text text = ui.Text(localeString, true, new Alignment?(Alignment.MiddleLeft), true, null);
            text.Slot.GetComponent<RectTransform>(null, false).AnchorMax.Value = new float2(0.25f, 1f);
            InteractionElement.ColorDriver colorDriver = text.Slot.AttachComponent<Button>(true, null).ColorDrivers.Add();
            colorDriver.ColorDrive.Target = text.Color;
            colorDriver.NormalColor.Value = color.Black;
            colorDriver.HighlightColor.Value = color.Blue;
            colorDriver.PressColor.Value = color.Blue;
            text.Slot.AttachComponent<ReferenceProxySource>(true, null).Reference.Target = target;
        }

        private static string funName(Type delegateType, MethodInfo info) =>
            string.Concat(new string[]
            {
            info.IsPublic ? "Public " : "Private ",
            info.IsStatic ? "Static " : "",
            info.IsAbstract ? "Abstract " : "",
            info.IsVirtual ? "Virtual " : "",
            info?.GetBaseDefinition() != info && (info.GetBaseDefinition().GetCustomAttributes(typeof(SyncMethod), true)?.Any() ?? false) ? "Override " : "",
            info.ReturnType.Name,
            " ",
            info.ToString().Substring(info.ToString().IndexOf(" ")).Replace("FrooxEngine.", ""),
            });

        [HarmonyPatch(typeof(WorkerInspector))]
        [HarmonyPatch("BuildInspectorUI")]
        class WorkerInspector_BuildInspectorUI_Patch
        {
            private static void Postfix(WorkerInspector __instance, Worker worker, UIBuilder ui, Predicate<ISyncMember> memberFilter = null)
            {
                if (config.GetValue(KEY_SHOW_HIDDEN))
                    for (int i = 0; i < worker.SyncMemberCount; i++)
                    {
                        ISyncMember syncMember = worker.GetSyncMember(i);
                        if (worker.GetSyncMemberFieldInfo(i).GetCustomAttribute<HideInInspectorAttribute>() != null)
                        {
                            GenerateReferenceProxy(ui, worker.GetSyncMemberName(i), syncMember);
                        }
                    }
                if (!config.GetValue(KEY_SHOW_DELEGATES)) return;
                BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
                List<MethodInfo> list = worker.GetType().GetMethods(bindingAttr).ToList<MethodInfo>();
                list.AddRange(worker.GetType().BaseType.GetMethods(bindingAttr).ToArray<MethodInfo>());
                list = (from m in list
                        where m.GetParameters().Length <= 3 && m.GetCustomAttributes(typeof(SyncMethod), true).Any<object>()
                        select m).ToList<MethodInfo>();
                if (list.Count != 0)
                {
                    var myTxt = ui.Text("---- SYNC METHODS HERE ----", true, new Alignment?(Alignment.MiddleCenter), true, null);
                    var delegates = ui.VerticalLayout();
                    delegates.Slot.ActiveSelf = false;
                    delegates.Slot.RemoveComponent(delegates.Slot.GetComponent<LayoutElement>());
                    var expander = myTxt.Slot.AttachComponent<Expander>();
                    expander.SectionRoot.Target = delegates.Slot;
                    expander.IsExpanded = config.GetValue(KEY_DEFAULT_OPEN);
                    var colorDriver = myTxt.Slot.AttachComponent<Button>().ColorDrivers.Add();
                    colorDriver.ColorDrive.Target = myTxt.Color;
                    colorDriver.NormalColor.Value = color.Black;
                    colorDriver.HighlightColor.Value = new color(0.4f);
                    colorDriver.PressColor.Value = new color(0.7f);
                    foreach (MethodInfo methodInfo in list)
                    {
                        Type delegteType = null;
                        var param = methodInfo.GetParameters();
                        if(methodInfo.ReturnType == typeof(void))
                        {
                            if (param.Length == 0) delegteType = typeof(Action);
                            else delegteType = ActionType(param?.Length ?? 0).MakeGenericType(param.Types());
                        }
                        else
                        {
                            Type[] types = new Type[param.Length + 1];
                            for(int i = 0; i < param.Length; i++) types[i] = param[i].ParameterType;
                            types[param.Length] = methodInfo.ReturnType;
                            delegteType = FuncType(param?.Length ?? 0).MakeGenericType(types);
                        }

                        if (delegteType != null) GenerateGenericDelegateProxy(ui, funName(delegteType, methodInfo), methodInfo.CreateDelegate(delegteType, methodInfo.IsStatic ? null : worker), delegteType);
                    }
                    ui.NestOut();
                }
            }
            static Type ActionType(int argCount) => argCount switch
            {
                1 => typeof(Action<>),
                2 => typeof(Action<,>),
                3 => typeof(Action<,,>),
                4 => typeof(Action<,,,>),
                5 => typeof(Action<,,,,>),
                6 => typeof(Action<,,,,,>),
                7 => typeof(Action<,,,,,,>),
                8 => typeof(Action<,,,,,,,>),
                9 => typeof(Action<,,,,,,,,>),
                10 => typeof(Action<,,,,,,,,,>),
                11 => typeof(Action<,,,,,,,,,,>),
                12 => typeof(Action<,,,,,,,,,,,>),
                13 => typeof(Action<,,,,,,,,,,,,>),
                14 => typeof(Action<,,,,,,,,,,,,,>),
                15 => typeof(Action<,,,,,,,,,,,,,,>),
                16 => typeof(Action<,,,,,,,,,,,,,,,>)
            };
            static Type FuncType(int argCount) => argCount switch
            {
                0 => typeof(Func<>),
                1 => typeof(Func<,>),
                2 => typeof(Func<,,>),
                3 => typeof(Func<,,,>),
                4 => typeof(Func<,,,,>),
                5 => typeof(Func<,,,,,>),
                6 => typeof(Func<,,,,,,>),
                7 => typeof(Func<,,,,,,,>),
                8 => typeof(Func<,,,,,,,,>),
                9 => typeof(Func<,,,,,,,,,>),
                10 => typeof(Func<,,,,,,,,,,>),
                11 => typeof(Func<,,,,,,,,,,,>),
                12 => typeof(Func<,,,,,,,,,,,,>),
                13 => typeof(Func<,,,,,,,,,,,,,>),
                14 => typeof(Func<,,,,,,,,,,,,,,>),
                15 => typeof(Func<,,,,,,,,,,,,,,,>),
                16 => typeof(Func<,,,,,,,,,,,,,,,,>)
            };
        }
    }
}