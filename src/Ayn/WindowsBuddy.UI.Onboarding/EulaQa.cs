using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Input;
using System.Xml;
using Microsoft.Win32;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.Services;
using WindowsBuddy.UI.Settings;

namespace WindowsBuddy.UI.Onboarding;

internal static class EulaQa
{
	public static readonly string[] ThemeOrder = new string[4] { "PureBlack", "Vivid", "EmberDark", "Silver" };

	internal static Dictionary<string, string> Snapshot(string root)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		if (!Directory.Exists(root))
		{
			return dictionary;
		}
		string[] files = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
		foreach (string text in files)
		{
			string text2 = text.Substring(root.Length).TrimStart('\\');
			if (IsDiagnostic(text2))
			{
				continue;
			}
			try
			{
				using FileStream inputStream = new FileStream(text, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
				using SHA256 sHA = SHA256.Create();
				dictionary[text2] = BitConverter.ToString(sHA.ComputeHash(inputStream));
			}
			catch (Exception ex)
			{
				dictionary[text2] = "unreadable " + ex.GetType().Name;
			}
		}
		return dictionary;
	}

	internal static bool IsDiagnostic(string rel)
	{
		string text = rel.ToLowerInvariant();
		if (!text.StartsWith("logs\\") && !text.StartsWith("diagnostics\\") && !text.StartsWith("crash\\") && !text.StartsWith("aynjit\\") && !text.StartsWith("fonts\\"))
		{
			return text.EndsWith(".log");
		}
		return true;
	}

	internal static void ResetSandboxAyn(StringBuilder report)
	{
		if (!AppPaths.IsSandboxed || string.IsNullOrEmpty(AppPaths.InstanceSuffix))
		{
			return;
		}
		string text = AynRegistryKey();
		if (!text.Contains("_qa"))
		{
			return;
		}
		try
		{
			Registry.CurrentUser.DeleteSubKeyTree(text, throwOnMissingSubKey: false);
			report.AppendLine("Reset sandbox trial key HKCU\\" + text);
		}
		catch (Exception ex)
		{
			report.AppendLine("Could not reset sandbox trial key: " + ex.Message);
		}
	}

	internal static string Diff(Dictionary<string, string> a, Dictionary<string, string> b)
	{
		List<string> list = new List<string>();
		foreach (string item in from x in a.Keys.Union(b.Keys, StringComparer.OrdinalIgnoreCase)
			orderby x
			select x)
		{
			string value;
			bool flag = a.TryGetValue(item, out value);
			string value2;
			bool flag2 = b.TryGetValue(item, out value2);
			if (!flag)
			{
				list.Add("+" + item);
			}
			else if (!flag2)
			{
				list.Add("-" + item);
			}
			else if (value != value2)
			{
				list.Add("~" + item);
			}
		}
		if (list.Count != 0)
		{
			return string.Join(", ", list);
		}
		return "";
	}

	private static string AynRegistryKey()
	{
		return "Software\\ModiStudio\\Ayn" + AppPaths.InstanceSuffix;
	}

	internal static bool AynRegistryExists()
	{
		using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(AynRegistryKey(), writable: false);
		return registryKey != null;
	}

	public static void Static(Action<bool, string> check, StringBuilder report)
	{
		string text = FindDocx();
		check(text != null, "EULA source document found (" + (text ?? "docs\\EULA_1.1.62\\Ayn_EULA_v1.0.docx missing") + ")");
		if (text != null)
		{
			List<KeyValuePair<string, string>> list = ReadDocx(text);
			EulaBlock[] blocks = EulaDocument.Blocks;
			int num = -1;
			for (int i = 0; i < Math.Max(list.Count, blocks.Length); i++)
			{
				if (i >= list.Count || i >= blocks.Length || list[i].Key != blocks[i].Kind.ToString() || list[i].Value != blocks[i].Text)
				{
					num = i;
					break;
				}
			}
			check(list.Count == blocks.Length && num < 0, string.Format("EULA text in the app matches the approved document word for word ({0} blocks in the app, {1} in the document{2})", blocks.Length, list.Count, (num < 0) ? "" : (", first difference at block " + num + ": '" + ((num < blocks.Length) ? blocks[num].Text : "-") + "'")));
			check(blocks.Length > 0 && blocks[0].Kind == EulaBlockKind.H1 && blocks[0].Text == "Ayn End User License Agreement" && blocks.Any((EulaBlock b) => b.Text.StartsWith("Version 1.0 ")), "EULA starts with its title and the Version 1.0 line");
			check(!blocks.Any((EulaBlock b) => b.Text.Contains("drafting aid") || b.Text.Contains("Part 2") || b.Text.Contains("for the developer")), "drafting note and Part 2 (developer notes) are not shown to users");
			check(blocks.Last().Text.Contains("contact@modistudio.online") && blocks.Count((EulaBlock b) => b.Kind == EulaBlockKind.H2) == 20, "all 20 sections, ending with 20. Contact");
		}
		check(arg1: true, "agreement version is 1.0");
		check(EulaAgreement.NeedsAgreement(null, "1.0") && EulaAgreement.NeedsAgreement("", "1.0"), "no record: agreement needed");
		check(!EulaAgreement.NeedsAgreement("1.0", "1.0"), "accepted 1.0, build asks 1.0: not asked again");
		check(EulaAgreement.NeedsAgreement("1.0", "1.1") && EulaAgreement.NeedsAgreement("1.0", "2.0"), "accepted 1.0, build asks 1.1 / 2.0: asked again (version bump)");
		var array = new[]
		{
			new
			{
				Name = "1366x768 @100%",
				Area = new Rect(0.0, 0.0, 1366.0, 728.0)
			},
			new
			{
				Name = "1920x1080 @125%",
				Area = new Rect(0.0, 0.0, 1536.0, 830.0)
			},
			new
			{
				Name = "1920x1080 @150%",
				Area = new Rect(0.0, 0.0, 1280.0, 688.0)
			},
			new
			{
				Name = "1920x1080 @175%",
				Area = new Rect(0.0, 0.0, 1097.0, 590.0)
			},
			new
			{
				Name = "1920x1080 @200%",
				Area = new Rect(0.0, 0.0, 960.0, 516.0)
			},
			new
			{
				Name = "2560x1440 @200%",
				Area = new Rect(0.0, 0.0, 1280.0, 696.0)
			},
			new
			{
				Name = "3840x2160 @200%",
				Area = new Rect(0.0, 0.0, 1920.0, 1056.0)
			}
		};
		var array2 = array;
		foreach (var anon in array2)
		{
			var array3 = new[]
			{
				new
				{
					N = "tour",
					W = 760.0,
					H = 540.0
				},
				new
				{
					N = "one-time screen",
					W = 640.0,
					H = 460.0
				},
				new
				{
					N = "EULA reader",
					W = 680.0,
					H = 640.0
				}
			};
			foreach (var anon2 in array3)
			{
				double num2 = AgreementPanel.FitScale(anon2.W, anon2.H, anon.Area);
				bool flag = anon2.W * num2 <= anon.Area.Width && anon2.H * num2 <= anon.Area.Height;
				check(flag && num2 >= 0.75, $"{anon.Name}: {anon2.N} fits ({anon2.W * num2:0} x {anon2.H * num2:0} DIP, scale {num2:0.00})");
			}
		}
	}

	private static string FindDocx()
	{
		string environmentVariable = Environment.GetEnvironmentVariable("VAYLI_QA_EULA_DOCX");
		if (!string.IsNullOrEmpty(environmentVariable) && File.Exists(environmentVariable))
		{
			return environmentVariable;
		}
		string text = AppDomain.CurrentDomain.BaseDirectory;
		for (int i = 0; i < 4; i++)
		{
			if (text == null)
			{
				break;
			}
			string text2 = Path.Combine(text, "docs", "EULA_1.1.62", "Ayn_EULA_v1.0.docx");
			if (File.Exists(text2))
			{
				return text2;
			}
			text = Path.GetDirectoryName(text.TrimEnd('\\'));
		}
		return null;
	}

	internal static List<KeyValuePair<string, string>> ReadDocx(string path)
	{
		List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
		using (ZipArchive zipArchive = ZipFile.OpenRead(path))
		{
			using Stream inStream = zipArchive.GetEntry("word/document.xml").Open();
			XmlDocument xmlDocument = new XmlDocument();
			xmlDocument.Load(inStream);
			XmlNamespaceManager xmlNamespaceManager = new XmlNamespaceManager(xmlDocument.NameTable);
			xmlNamespaceManager.AddNamespace("w", "http://schemas.openxmlformats.org/wordprocessingml/2006/main");
			foreach (XmlNode item in xmlDocument.SelectNodes("//w:body/w:p", xmlNamespaceManager))
			{
				XmlNode xmlNode2 = item.SelectSingleNode("w:pPr/w:pStyle/@w:val", xmlNamespaceManager);
				bool flag = item.SelectSingleNode("w:pPr/w:numPr", xmlNamespaceManager) != null;
				StringBuilder stringBuilder = new StringBuilder();
				foreach (XmlNode item2 in item.SelectNodes(".//w:t", xmlNamespaceManager))
				{
					stringBuilder.Append(item2.InnerText);
				}
				string text = stringBuilder.ToString();
				if (text.Trim().Length != 0)
				{
					if (text.StartsWith("Part 2."))
					{
						break;
					}
					if (!text.StartsWith("This is a drafting aid"))
					{
						string key = ((xmlNode2 != null && xmlNode2.Value == "Heading1") ? "H1" : ((xmlNode2 != null && xmlNode2.Value == "Heading2") ? "H2" : (flag ? "LI" : "P")));
						list.Add(new KeyValuePair<string, string>(key, text));
					}
				}
			}
		}
		return list;
	}

	public static void Screens(Action<bool, string> check, StringBuilder report, string shots)
	{
		Directory.CreateDirectory(shots);
		NotchConfig instance = NotchConfig.Instance;
		List<string> opened = new List<string>();
		int readerOpened = 0;
		Action<string> openUrl = AgreementPanel.OpenUrl;
		Action<Window> openEula = AgreementPanel.OpenEula;
		AgreementPanel.OpenUrl = delegate(string u)
		{
			opened.Add(u);
		};
		AgreementPanel.OpenEula = delegate
		{
			readerOpened++;
		};
		try
		{
			string dataDir = AppPaths.DataDir;
			check(!EulaAgreement.IsAccepted && EulaAgreement.ReadAcceptedVersion() == null, "fresh profile: agreement not accepted");
			Dictionary<string, string> dictionary = Snapshot(dataDir);
			bool flag = AynRegistryExists();
			report.AppendLine("  profile before the agreement: " + ((dictionary.Count == 0) ? "(no files)" : string.Join(", ", dictionary.Keys.OrderBy((string k) => k))));
			int started = 0;
			int declined = 0;
			Func<CapsuleWindow> startNotch = delegate
			{
				started++;
				return (CapsuleWindow)null;
			};
			OnboardingWindow onboardingWindow = new OnboardingWindow(delegate
			{
			}, startNotch, delegate
			{
				declined++;
			});
			onboardingWindow.Show();
			onboardingWindow.Activate();
			OnboardingQa.Pump(700);
			string text = OnboardingQa.PageText(onboardingWindow);
			check(onboardingWindow.IsAgreementShowing && onboardingWindow.Step == -1, "tour opens on the agreement (step " + onboardingWindow.Step + ")");
			check(text.Contains("Before you start") && text.Contains("To use Ayn, please read and accept its End User License Agreement."), "agreement: title 'Before you start' and one plain sentence");
			check(text.Contains("I have read and agree to the Ayn End User License Agreement"), "agreement: checkbox 'I have read and agree to the Ayn End User License Agreement'");
			check(text.Contains("End User License Agreement") && text.Contains("Terms of Service") && text.Contains("Privacy Policy"), "agreement: links to the EULA, Terms of Service and Privacy Policy");
			SButton sButton = OnboardingQa.FindButton(onboardingWindow, "Accept and continue");
			SButton sButton2 = OnboardingQa.FindButton(onboardingWindow, "Decline and exit");
			check(sButton != null && sButton2 != null, "agreement: 'Accept and continue' and 'Decline and exit' buttons");
			check(sButton != null && !sButton.IsEnabled, "Accept and continue is disabled until the box is ticked");
			check(OnboardingQa.FindButton(onboardingWindow, "Get started") == null && OnboardingQa.FindButton(onboardingWindow, "Skip") == null && OnboardingQa.FindButton(onboardingWindow, "Back") == null, "agreement: no Skip / Back / Get started (it cannot be skipped)");
			SCheckBox agreementBox = onboardingWindow.AgreementBox;
			check(agreementBox != null && !agreementBox.IsChecked, "the box starts unticked");
			check(agreementBox != null && Keyboard.FocusedElement == agreementBox, "keyboard focus starts on the checkbox (" + ((Keyboard.FocusedElement != null) ? Keyboard.FocusedElement.GetType().Name : "none") + ")");
			foreach (TrackedText item in OnboardingQa.FindAll<TrackedText>(onboardingWindow))
			{
				check(item.FamilyName.StartsWith("Geist"), "agreement headline '" + item.Text + "' is set in Geist (" + item.FamilyName + ")");
			}
			OnboardingQa.Press(onboardingWindow, Key.Return);
			OnboardingQa.Pump();
			OnboardingQa.Press(onboardingWindow, Key.Right);
			OnboardingQa.Pump(200);
			onboardingWindow.Go(1);
			onboardingWindow.Go(3);
			OnboardingQa.Pump(200);
			onboardingWindow.Finish();
			OnboardingQa.Pump(200);
			onboardingWindow.AcceptAgreement();
			OnboardingQa.Pump(200);
			sButton?.RaiseClick();
			OnboardingQa.Pump();
			check(onboardingWindow.IsAgreementShowing && onboardingWindow.IsVisible && started == 0 && !EulaAgreement.IsAccepted, "without ticking: Enter, Right, Go(), Finish(), Accept and a click on the disabled button all stay on the agreement (notch starts: " + started + ")");
			Peer(check, agreementBox, AutomationControlType.CheckBox, "I have read and agree to the Ayn End User License Agreement");
			foreach (SLink item2 in OnboardingQa.FindAll<SLink>(onboardingWindow))
			{
				Peer(check, item2, AutomationControlType.Hyperlink, item2.Text);
			}
			Peer(check, sButton, AutomationControlType.Button, "Accept and continue");
			Peer(check, sButton2, AutomationControlType.Button, "Decline and exit");
			SClickable sClickable = OnboardingQa.FindAll<SClickable>(onboardingWindow).FirstOrDefault((SClickable c) => AutomationProperties.GetName(c) == "Close (decline and exit)");
			check(sClickable != null, "close (X) button is named 'Close (decline and exit)' while the agreement is open");
			AutomationPeer automationPeer = ((sButton != null) ? UIElementAutomationPeer.CreatePeerForElement(sButton) : null);
			check(automationPeer != null && !automationPeer.IsEnabled(), "screen readers report Accept as unavailable until ticked");
			foreach (FrameworkElement item3 in new FrameworkElement[4] { agreementBox, sButton, sButton2, sClickable }.Concat(OnboardingQa.FindAll<SLink>(onboardingWindow)))
			{
				check(item3 != null && item3.Focusable && item3.FocusVisualStyle != null, "focus ring on '" + ((item3 != null) ? AutomationProperties.GetName(item3) : "-") + "'");
			}
			string text2 = TabOrder(onboardingWindow, agreementBox, 7);
			string text3 = string.Join(" > ", "End User License Agreement", "Terms of Service", "Privacy Policy", "Decline and exit", "Close (decline and exit)", "I have read and agree to the Ayn End User License Agreement", "End User License Agreement");
			check(text2 == text3, "Tab order (unticked): " + text2);
			agreementBox.Focus();
			OnboardingQa.Pump(100);
			KeyOn(onboardingWindow, agreementBox, Key.Space);
			OnboardingQa.Pump(200);
			check(agreementBox.IsChecked && sButton.IsEnabled, "Space ticks the box and enables Accept and continue");
			string text4 = TabOrder(onboardingWindow, agreementBox, 7);
			check(text4.Contains("Decline and exit > Accept and continue"), "Tab order (ticked) includes Accept after Decline: " + text4);
			agreementBox.Focus();
			OnboardingQa.Pump(100);
			KeyOn(onboardingWindow, agreementBox, Key.Space);
			OnboardingQa.Pump(200);
			check(!agreementBox.IsChecked && !sButton.IsEnabled, "Space again unticks it and disables Accept");
			IToggleProvider toggleProvider = UIElementAutomationPeer.CreatePeerForElement(agreementBox).GetPattern(PatternInterface.Toggle) as IToggleProvider;
			toggleProvider?.Toggle();
			OnboardingQa.Pump(200);
			check(toggleProvider != null && agreementBox.IsChecked && toggleProvider.ToggleState == ToggleState.On, "screen-reader Toggle ticks it too");
			foreach (SLink item4 in OnboardingQa.FindAll<SLink>(onboardingWindow))
			{
				item4.RaiseClick();
			}
			OnboardingQa.Pump(200);
			check(readerOpened == 1, "End User License Agreement link opens the in-app reader");
			check(opened.SequenceEqual(new string[2] { "https://github.com/salahuddinselim/Ayn", "https://github.com/salahuddinselim/Ayn" }), "Terms and Privacy links open https://github.com/salahuddinselim/Ayn and https://github.com/salahuddinselim/Ayn (" + string.Join(", ", opened) + ")");
			check(arg1: true, "link addresses: terms, privacy and eula on github.com/salahuddinselim/Ayn");
			OnboardingQa.Capture(onboardingWindow, Path.Combine(shots, "tour-agreement_ticked_PureBlack.png"));
			sButton2?.RaiseClick();
			OnboardingQa.Pump(600);
			check(!onboardingWindow.IsVisible && onboardingWindow.Declined && declined == 1, "Decline and exit closes the tour and asks Ayn to exit (exit callback: " + declined + ")");
			check(started == 0, "declining never started the notch (or the trial with it)");
			OnboardingWindow onboardingWindow2 = new OnboardingWindow(delegate
			{
			}, startNotch, delegate
			{
				declined++;
			});
			onboardingWindow2.Show();
			onboardingWindow2.Activate();
			OnboardingQa.Pump(500);
			OnboardingQa.Press(onboardingWindow2, Key.Escape);
			OnboardingQa.Pump(500);
			check(!onboardingWindow2.IsVisible && onboardingWindow2.Declined && declined == 2, "Esc on the agreement = Decline and exit");
			OnboardingWindow onboardingWindow3 = new OnboardingWindow(delegate
			{
			}, startNotch, delegate
			{
				declined++;
			});
			onboardingWindow3.Show();
			OnboardingQa.Pump(400);
			onboardingWindow3.Close();
			OnboardingQa.Pump(400);
			check(!onboardingWindow3.IsVisible && onboardingWindow3.Declined && declined == 3, "Alt+F4 / closing the window = Decline and exit");
			AgreementWindow agreementWindow = new AgreementWindow();
			agreementWindow.Show();
			agreementWindow.Activate();
			OnboardingQa.Pump(600);
			string text5 = OnboardingQa.PageText(agreementWindow);
			check(text5.Contains("Before you continue") && text5.Contains("Ayn now has an End User License Agreement, so please read and accept it to keep using Ayn.") && text5.Contains("I have read and agree to the Ayn End User License Agreement") && text5.Contains("End User License Agreement") && text5.Contains("Terms of Service") && text5.Contains("Privacy Policy"), "one-time screen: title, sentence, the same checkbox and links");
			check(agreementWindow.AcceptButton.Text == "Accept and continue" && !agreementWindow.AcceptButton.IsEnabled && agreementWindow.DeclineButton.Text == "Decline and exit", "one-time screen: Accept disabled until ticked, Decline and exit");
			check(Keyboard.FocusedElement == agreementWindow.CheckBox, "one-time screen: focus starts on the checkbox");
			OnboardingQa.Press(agreementWindow, Key.Return);
			OnboardingQa.Pump(200);
			agreementWindow.Decide(accept: true);
			OnboardingQa.Pump(200);
			check(agreementWindow.IsVisible && !agreementWindow.Accepted && !EulaAgreement.IsAccepted, "one-time screen: Enter / Accept without ticking does nothing");
			check(TabOrder(agreementWindow, agreementWindow.CheckBox, 7) == text3, "one-time screen: same Tab order");
			Peer(check, agreementWindow.CheckBox, AutomationControlType.CheckBox, "I have read and agree to the Ayn End User License Agreement");
			agreementWindow.DeclineButton.RaiseClick();
			OnboardingQa.Pump(400);
			check(!agreementWindow.IsVisible && !agreementWindow.Accepted, "one-time screen: Decline and exit closes it (not accepted)");
			Dictionary<string, string> dictionary2 = Snapshot(dataDir);
			string text6 = Diff(dictionary, dictionary2);
			check(text6 == "", "decline wrote nothing to the profile" + ((text6 == "") ? (" (" + dictionary2.Count + " files, unchanged)") : (": " + text6)));
			check(!EulaAgreement.IsAccepted, "after declining: still not accepted");
			check(!File.Exists(Path.Combine(dataDir, "firstrun.done")) && !File.Exists(Path.Combine(dataDir, "startup.initialized")), "after declining: no first-run marker");
			check(flag == AynRegistryExists() && !AynRegistryExists(), "after declining: no trial record in the registry");
			check(!LicenseService.Instance.AynStarted, "after declining: no trial started");
			EulaReaderWindow eulaReaderWindow = new EulaReaderWindow();
			eulaReaderWindow.Show();
			OnboardingQa.Pump(600);
			string rt = OnboardingQa.PageText(eulaReaderWindow);
			int num = EulaDocument.Blocks.Count((EulaBlock b) => b.Kind != 0 && rt.Contains(b.Text));
			check(num == EulaDocument.Blocks.Length - 1, "reader shows every paragraph of the agreement (" + num + " of " + (EulaDocument.Blocks.Length - 1) + ")");
			check(eulaReaderWindow.Scroll.ScrollableHeight > 1000.0, "reader scrolls (" + eulaReaderWindow.Scroll.ScrollableHeight.ToString("0") + " DIP of text below the fold)");
			OnboardingQa.FindAll<SLink>(eulaReaderWindow).FirstOrDefault()?.RaiseClick();
			OnboardingQa.Pump(200);
			check(opened.LastOrDefault() == "https://github.com/salahuddinselim/Ayn", "reader link opens https://github.com/salahuddinselim/Ayn (" + opened.LastOrDefault() + ")");
			check(Keyboard.FocusedElement == eulaReaderWindow.Scroll, "reader: focus starts on the text, so arrows and Page Down scroll it");
			OnboardingQa.Press(eulaReaderWindow, Key.Escape);
			OnboardingQa.Pump();
			check(!eulaReaderWindow.IsVisible, "reader: Esc closes it");
			string[] themeOrder = ThemeOrder;
			for (int i = 0; i < themeOrder.Length; i++)
			{
				string text8 = (instance.AppTheme = themeOrder[i]);
				OnboardingQa.Pump(500);
				OnboardingWindow onboardingWindow4 = new OnboardingWindow(delegate
				{
				}, startNotch, delegate
				{
				});
				onboardingWindow4.Show();
				onboardingWindow4.Activate();
				OnboardingQa.Pump(600);
				Shot(check, report, onboardingWindow4, Path.Combine(shots, "tour-agreement_" + text8 + ".png"), "tour agreement " + text8);
				onboardingWindow4.Decline();
				OnboardingQa.Pump();
				AgreementWindow agreementWindow2 = new AgreementWindow();
				agreementWindow2.Show();
				agreementWindow2.Activate();
				OnboardingQa.Pump(600);
				Shot(check, report, agreementWindow2, Path.Combine(shots, "one-time_" + text8 + ".png"), "one-time screen " + text8);
				agreementWindow2.Decide(accept: false);
				OnboardingQa.Pump();
				EulaReaderWindow eulaReaderWindow2 = new EulaReaderWindow();
				eulaReaderWindow2.Show();
				OnboardingQa.Pump(600);
				Shot(check, report, eulaReaderWindow2, Path.Combine(shots, "reader_" + text8 + ".png"), "EULA reader " + text8);
				eulaReaderWindow2.Close();
				OnboardingQa.Pump(200);
			}
			instance.AppTheme = "PureBlack";
			OnboardingQa.Pump(400);
			check(started == 0 && !EulaAgreement.IsAccepted, "after every agreement check: notch never started, agreement still not accepted");
		}
		finally
		{
			AgreementPanel.OpenUrl = openUrl;
			AgreementPanel.OpenEula = openEula;
		}
	}

	public static void AfterAccept(Action<bool, string> check)
	{
		Dictionary<string, object> dictionary = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(EulaAgreement.PreferencesPath)) as Dictionary<string, object>;
		dictionary.TryGetValue("EulaAcceptedVersion", out var value);
		dictionary.TryGetValue("EulaAcceptedUtc", out var value2);
		dictionary.TryGetValue("EulaAcceptedAppVersion", out var value3);
		DateTime result;
		bool flag = value2 is string && DateTime.TryParse((string)value2, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out result) && Math.Abs((DateTime.UtcNow - result.ToUniversalTime()).TotalMinutes) < 10.0;
		check(value as string == "1.0" && flag && value3 as string == SettingsPages.ShortVersion(), string.Concat("acceptance stored in settings\\app_preferences.json: version ", value, ", ", value2, ", Ayn ", value3));
		check(EulaAgreement.IsAccepted, "accepted: not asked again for 1.0");
		EulaAgreement.RecordAcceptance();
		check(EulaAgreement.IsAccepted && EulaAgreement.ReadAcceptedVersion() == "1.0", "back on 1.0 for the rest of the tour checks");
	}

	private static void Shot(Action<bool, string> check, StringBuilder report, Window win, string path, string label)
	{
		OnboardingQa.Capture(win, path);
		List<string> list = ContrastScan.Scan(win, "eula/" + label);
		foreach (string item in list)
		{
			report.AppendLine("  " + item);
		}
		check(list.Count == 0, label + ": no text below 3:1 contrast (" + list.Count + " found)");
		string text = OnboardingQa.Clipped(win);
		check(text == null, label + ": nothing clipped or outside the window" + ((text != null) ? (" (" + text + ")") : ""));
	}

	private static void Peer(Action<bool, string> check, UIElement el, AutomationControlType type, string name)
	{
		AutomationPeer automationPeer = ((el != null) ? UIElementAutomationPeer.CreatePeerForElement(el) : null);
		check(automationPeer != null && automationPeer.GetAutomationControlType() == type && automationPeer.GetName() == name && (automationPeer.IsKeyboardFocusable() || !el.IsEnabled), string.Concat("screen reader: ", type, " '", name, "' (", (automationPeer != null) ? string.Concat(automationPeer.GetAutomationControlType(), " '", automationPeer.GetName(), "'") : "no peer", ")"));
	}

	private static string TabOrder(Window w, UIElement start, int count)
	{
		start.Focus();
		OnboardingQa.Pump(80);
		List<string> list = new List<string>();
		for (int i = 0; i < count; i++)
		{
			if (!(Keyboard.FocusedElement is UIElement uIElement))
			{
				break;
			}
			uIElement.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
			OnboardingQa.Pump(40);
			DependencyObject dependencyObject = Keyboard.FocusedElement as DependencyObject;
			list.Add((dependencyObject != null) ? AutomationProperties.GetName(dependencyObject) : "(none)");
		}
		return string.Join(" > ", list);
	}

	private static void KeyOn(Window w, UIElement el, Key key)
	{
		PresentationSource inputSource = PresentationSource.FromVisual(el);
		KeyEventArgs keyEventArgs = new KeyEventArgs(Keyboard.PrimaryDevice, inputSource, Environment.TickCount, key);
		keyEventArgs.RoutedEvent = Keyboard.PreviewKeyDownEvent;
		KeyEventArgs keyEventArgs2 = keyEventArgs;
		el.RaiseEvent(keyEventArgs2);
		if (!keyEventArgs2.Handled)
		{
			el.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, inputSource, Environment.TickCount, key)
			{
				RoutedEvent = Keyboard.KeyDownEvent
			});
			el.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, inputSource, Environment.TickCount, key)
			{
				RoutedEvent = Keyboard.KeyUpEvent
			});
		}
	}
}
