using System;
using System.Collections.Generic;
using System.IO;
using Unity.AppUI.UI;
using System.Linq;
using Unity.Cloud.Collaboration;
using Unity.Industry.Viewer.Shared;
using UnityEngine.UIElements;
using Unity.AppUI.Core;
using System.Text.RegularExpressions;
using UnityEngine;
using Unity.Cloud.Common;
using Unity.Industry.Viewer.Assets;
using Unity.Cloud.Identity;
using System.Threading.Tasks;
using System.Threading;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Unity.Industry.Viewer.Collaboration
{
    public enum AddAttachmentFailType
    {
        None,
        DuplicateFilePath,
        DuplicateFileName
    }
    
    public static class CollaborationUIUtility
    {
        private static readonly string[] Colors = new string[]
        {
            "#E54D2E", // tomatoDark.tomato9
            "#00A2C7", // cyanDark.cyan9
            "#F76B15", // orangeDark.orange9
            "#8E4EC6", // violetDark.violet9
            "#12A594", // tealDark.teal9
            "#46A758", // greenDark.green9
            "#AB4ABA"  // plumDark.plum9
        };
        
        public static Dictionary<string, string> ReactionData = new Dictionary<string, string>()
        {
            { "thumbup", "👍" },
            { "thumbdown", "👎" },
            { "celebrate", "🎉" },
            { "look" , "👀" },
            { "hundred", "💯" },
            { "check", "✅" },
            { "fire", "🔥" },
            { "smiley", "😀" }
        };

        public static Popover NamePopover;
        
        private static Dictionary<OrganizationId, IOrganization> m_AllOrganizations;
        private static Dictionary<OrganizationId, List<IUserInfo>> m_OrganizationMembers;
        private static bool shouldIgnoreKey;
        private static bool gettingMembers;

        public static bool JustDismissedPopover
        {
            get => _justDismissed;
            set
            {
                _justDismissed = value;
                if (_justDismissed)
                {
                    // Reset after a short delay to allow immediate re-triggering
                    Task.Delay(250).ContinueWith(_ => _justDismissed = false);
                }
            }
        }
        private static bool _justDismissed;

        private static void MakeNameSuggestionButton(TextArea textArea, Popover popover, VisualElement parent, List<IUserInfo> suggestedNames)
        {
            bool first = true;
            foreach (var userDataKeyName in suggestedNames)
            {
                var nameButton = new NameSuggestionButton
                {
                    selected = first
                };
                first = false;
                var newUserButton = new UserTaggingButtonController(
                    userDataKeyName, 
                    nameButton, textArea, ref popover);
                nameButton.userData = newUserButton;
                
                parent.Add(nameButton);
            }
        }

        public static void InsertNameTagging(IUserInfo userInfo, TextArea textArea)
        {
            var anchorIndex = 0;
            string searchValue = string.Empty;
            if (textArea.userData is (int i, string s))
            {
                anchorIndex = i;
                searchValue = s;
            }
            var textField = textArea.Q<UnityEngine.UIElements.TextField>();
            var currentValue = textField.value;
            // Remove the "@" before anchorIndex if present
            int totalReduceLength = 1;
            if (!string.IsNullOrEmpty(searchValue))
            {
                totalReduceLength += searchValue.Length;
            }
            
            if (anchorIndex > 0 && currentValue[anchorIndex - 1] == '@')
            {
                anchorIndex -= 1;
                currentValue = currentValue.Remove(anchorIndex, totalReduceLength);
            }

            var pattern = $":user[{userInfo.Name}]{{#{userInfo.UserId}}}";
            var tag = $"<color=#0070CA><link=\"{pattern}\">@{userInfo.Name}</link></color>";
            var newText = currentValue.Substring(0, anchorIndex) + tag + currentValue.Substring(anchorIndex);
            textArea.SetValueWithoutNotify(newText);
            
            var insertedLength = 0;
            var visibleInsertedText = $"@{userInfo.Name}";
            insertedLength = visibleInsertedText.Length;
            
            var newCursorPos = anchorIndex + insertedLength;
            textField.selectIndex = newCursorPos;
            textField.cursorIndex = newCursorPos;
        }

        private static string CleanMismatchedUserTags(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            const string openingTag = "<color=#0070CA><link=";
            const string closingTag = "</link></color>";
            // Match both complete and incomplete tags
            var userTagPattern = @"<color=#0070CA><link="":user\[([^\]]+)\]\{#[^}]+\}"">@([^<]+)</link></color>?";

            return Regex.Replace(text, userTagPattern, match =>
            {
                var linkUsername = match.Groups[1].Value;
                var displayText = match.Groups[2].Value;
                var matchedTag = match.Value;

                bool tagMalformed = !matchedTag.StartsWith(openingTag) || !matchedTag.EndsWith(closingTag);
                if (!string.Equals(linkUsername, displayText, StringComparison.Ordinal) || tagMalformed)
                {
                    return "@" + displayText;
                }

                return match.Value;
            });
        }

        public static string ConvertUserTagsForCloud(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Pattern to match <color=#0070CA><link="userId">@username</link></color>
            var richTextPattern = @"<color=#0070CA><link=""([^""]+)"">@([^<]+)</link></color>";

            return Regex.Replace(text, richTextPattern, "$1");
        }
        
        public static string ParseUserTags(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Pattern to match :user[username]{#id}
            var userTagPattern = @":user\[([^\]]+)\]\{#[^}]+\}";
    
            return Regex.Replace(text, userTagPattern, "<color=#0070CA>@$1</color>");
        }
        
        public static bool RepeatedAttachment(GridView gridView, IAnnotation annotation, string filePath, out AddAttachmentFailType type)
        {
            var existingList = gridView.itemsSource as List<Attachment>;
            var fileName = Path.GetFileName(filePath);
            if (existingList != null)
            {
                if (existingList.Any(x =>
                        string.Equals(x.FilePath, filePath, StringComparison.CurrentCultureIgnoreCase)))
                {
                    type = AddAttachmentFailType.DuplicateFilePath;
                    return true;
                }
                if (existingList.Any(x =>
                        string.Equals(x.FileName, fileName, StringComparison.CurrentCultureIgnoreCase)))
                {
                    type = AddAttachmentFailType.DuplicateFileName;
                    return true;
                }
            }

            if (annotation == null || annotation.Attachments == null || annotation.Attachments.Count <= 0)
            {
                type = AddAttachmentFailType.None;
                return false;
            }
            
            foreach (var annotationAttachment in annotation.Attachments)
            {
                if (annotationAttachment is not IFileAttachment fileAttachment) continue;
                if (string.Equals(fileAttachment.FilePath, fileName))
                {
                    type = AddAttachmentFailType.DuplicateFileName;
                    return true;
                }
            }
            type = AddAttachmentFailType.None;
            return false;
        }

        //Currently not supported @username as Rich Text is not supported in InputField for UI Toolkit
        public static void OnTextAreaValueChanging(AssetInfo assetInfo, ChangingEvent<string> evt)
        {
            var textArea = evt.target as TextArea;
            if(textArea == null) return;
            var textField = textArea.Q<UnityEngine.UIElements.TextField>();
            int originalCursorIndex = textField.cursorIndex;
            int originalSelectIndex = textField.selectIndex;

            OrganizationId organizationId = assetInfo.Asset.Descriptor.OrganizationId;
            
            MoveInsertedTextAfterTag();
            
            var newCleanText = CleanMismatchedUserTags(evt.newValue);
            
            if (!string.Equals(newCleanText, evt.newValue, StringComparison.Ordinal))
            {
                evt.newValue = newCleanText;
                textArea?.SetValueWithoutNotify(newCleanText);
                
                // Restore cursor position after updating text
                textField.selectIndex = originalSelectIndex;
                textField.cursorIndex = originalCursorIndex;
            }
            
            if (string.IsNullOrEmpty(evt.newValue))
            {
                NamePopover?.Dismiss();
                return;
            }
            
            if (NamePopover != null)
            {
                int anchorIndex = 0;
                if (textArea.userData is (int idx, string _))
                    anchorIndex = idx;
                
                // Find all @name patterns not inside tags
                string text = evt.newValue;
                // Match @ followed by one or more words (with spaces), but not inside tags
                var matches = Regex.Matches(text, @"@([^\s<@]+)");

                string searchName = string.Empty;
                foreach (Match match in matches)
                {
                    int atIndex = match.Index;
                    // Check if this @ is inside a tag (e.g., <link=...>)
                    bool insideTag = false;
                    int openTag = text.LastIndexOf("<link", atIndex, StringComparison.OrdinalIgnoreCase);
                    int closeTag = text.LastIndexOf("</link>", atIndex, StringComparison.OrdinalIgnoreCase);
                    if (openTag != -1 && (closeTag == -1 || openTag > closeTag))
                        insideTag = true;

                    if (insideTag) continue;
                    searchName = match.Groups[1].Value; // Use the last valid match
                    textArea.userData = (anchorIndex, searchName);
                }

                if (!string.IsNullOrEmpty(searchName))
                {
                    var firstTenName = ReturnSuggestedNames(organizationId, searchName);

                    if (firstTenName.Count <= 1)
                    {
                        if (firstTenName.Count == 1)
                        {
                            //Auto-complete the name
                            InsertNameTagging(firstTenName[0], textArea);
                        }
                        NamePopover.Dismiss();
                        return;
                    }
                    
                    VisualElement parent = null;
                    foreach (var nameButton in NamePopover.containerView.Query<NameSuggestionButton>().ToList())
                    {
                        parent ??= nameButton.parent;
                        nameButton.RemoveFromHierarchy();
                    }
                    MakeNameSuggestionButton(textArea, NamePopover, parent, firstTenName);
                } else
                {
                    NamePopover.Dismiss();
                }
            }
            
            if(!textField.Q<TextElement>().enableRichText) return;
            
            // Check if a single "@" was just inserted
            if (evt.newValue.Length == evt.previousValue.Length + 1)
            {
                int diffIndex = 0;
                // Find the index where the new character was inserted
                while (diffIndex < evt.previousValue.Length && evt.previousValue[diffIndex] == evt.newValue[diffIndex])
                    diffIndex++;
                
                if (evt.newValue[diffIndex] == '@')
                {
                    int startIndex = diffIndex + 1;
                    if (m_OrganizationMembers == null)
                    {
                        _ = GetMemberAndSuggestNames(organizationId, startIndex, textArea);
                    }
                    else
                    {
                        ShowNameSuggestion(organizationId, startIndex, textArea);
                    }
                }
            }
            return;
            
            async Task GetMemberAndSuggestNames(OrganizationId organizationId, int startIndex, TextArea ta)
            {
                await GetOrgMembers(organizationId);
                ShowNameSuggestion(organizationId, startIndex, ta);
            }
            
            void MoveInsertedTextAfterTag()
            {
                // This function looks for changes made between the end of a user name tag and the closing </link></color> tag.
                // If any text was inserted in that range, it moves that text to be after the closing tag.
                // This ensures that any text typed after a tagged user name remains outside of the tag.
                const string closingTag = "</link></color>";
                string oldValue = evt.previousValue;
                string newValue = evt.newValue;

                int searchStart = 0;
                while (searchStart < oldValue.Length && searchStart < newValue.Length)
                {
                    int oldTagIndex = oldValue.IndexOf(closingTag, searchStart, StringComparison.Ordinal);
                    int newTagIndex = newValue.IndexOf(closingTag, searchStart, StringComparison.Ordinal);

                    if (oldTagIndex == -1 || newTagIndex == -1)
                        break;

                    // Find the difference range for this tag
                    int diffStart = searchStart;
                    while (diffStart < oldValue.Length && diffStart < newValue.Length && oldValue[diffStart] == newValue[diffStart])
                        diffStart++;

                    int oldTagEnd = oldTagIndex + closingTag.Length;

                    // Only proceed if the change is between the end of the user name and before the tag
                    if (diffStart >= oldTagIndex && diffStart < oldTagEnd)
                    {
                        // Extract inserted text
                        int insertEnd = newTagIndex;
                        string inserted = newValue.Substring(oldTagIndex, insertEnd - oldTagIndex);

                        // Remove inserted text from between and append after the tag
                        string before = newValue.Substring(0, oldTagIndex);
                        string after = newValue.Substring(newTagIndex + closingTag.Length);
                        string result = before + closingTag + inserted + after;

                        evt.newValue = result;
                        textArea.SetValueWithoutNotify(result);

                        // Move the cursor after the tag + inserted text
                        textField.cursorIndex = textField.selectIndex = (before + closingTag + inserted).Length;
                        textField.selectIndex = textField.cursorIndex;

                        // Update newValue for next iteration
                        newValue = result;
                    }

                    searchStart = newTagIndex + closingTag.Length;
                }
            }
            
            static void ShowNameSuggestion(OrganizationId organizationId, int startIndex, TextArea textArea)
            {
                if(NamePopover != null) return;
            
                var suggestedNames = ReturnSuggestedNames(organizationId, string.Empty);
            
                if(suggestedNames == null) return;

                VisualElement nameSuggestionMenu = new VisualElement();
                
                nameSuggestionMenu.AddToClassList("thread-Popover-menu");
                textArea.userData = (startIndex, string.Empty);
                NamePopover = Popover.Build(textArea, nameSuggestionMenu).SetArrowVisible(false)
                    .SetPlacement(PopoverPlacement.Start);
            
                MakeNameSuggestionButton(textArea, NamePopover, nameSuggestionMenu, suggestedNames);
            
                NamePopover.Show();
            
                NamePopover.shown += PopoverMenuOnShown;
                NamePopover.dismissed += NamePopoverOnDismissed;
                return;

                void PopoverMenuOnShown(Popover obj)
                {
                    NamePopover.shown -= PopoverMenuOnShown;
                    textArea.RegisterCallback<KeyDownEvent>(OnKeyDownWithPopover, TrickleDown.TrickleDown);
                    textArea.Focus();
                }
            
                void NamePopoverOnDismissed(Popover arg1, DismissType arg2)
                {
                    NamePopover.dismissed -= NamePopoverOnDismissed;
                    textArea.UnregisterCallback<KeyDownEvent>(OnKeyDownWithPopover, TrickleDown.TrickleDown);
                    JustDismissedPopover = true;
                    Object.FindAnyObjectByType<CollaborationUIBase>()?.ResetDismissedPopover();
                    NamePopover = null;
                }
            }
            
            static void OnKeyDownWithPopover(KeyDownEvent keydownEvt)
            {
                if (NamePopover == null) return;
                if (keydownEvt.keyCode is KeyCode.UpArrow or KeyCode.DownArrow)
                {
                    keydownEvt.StopPropagation();
                    var buttons = NamePopover.containerView.Query<ActionButton>().ToList();
                    if (buttons.Count == 0) return;
                    int currentIndex = -1;
                    for (int i = 0; i < buttons.Count; i++)
                    {
                        if (buttons[i].selected)
                        {
                            currentIndex = i;
                            break;
                        }
                    }
                    
                    // Calculate new index
                    if (keydownEvt.keyCode == KeyCode.UpArrow)
                    {
                        currentIndex = currentIndex <= 0 ? buttons.Count - 1: currentIndex - 1;
                    }
                    else // DownArrow
                    {
                        currentIndex = currentIndex >= buttons.Count - 1 ? 0: currentIndex + 1;
                    }

                    // Update button selection
                    for (int i = 0; i < buttons.Count; i++)
                    {
                        buttons[i].selected = i == currentIndex;
                    }
                    return;
                } 
                
                if (keydownEvt.keyCode == KeyCode.Return || keydownEvt.keyCode == KeyCode.KeypadEnter)
                {
                    keydownEvt.StopPropagation();
                    shouldIgnoreKey = true;
                    return;
                }
                
                if (shouldIgnoreKey && keydownEvt.keyCode == KeyCode.None)
                {
                    keydownEvt.StopPropagation();
                    var selectedButton = NamePopover.containerView.Query<ActionButton>()
                        .Where(b => b.selected).First();

                    if (selectedButton?.userData is not UserTaggingButtonController controller) return;
                    NamePopover?.Dismiss();
                    controller.Click();
                }
                shouldIgnoreKey = false;
            }
        }
        
        public static async Task<string> GetMemberName(OrganizationId orgId, string userId)
        {
            while(m_OrganizationMembers == null && gettingMembers)
            {
                await Task.Yield();
            }
            
            if (m_OrganizationMembers == null && !gettingMembers)
            {
                await GetOrgMembers(orgId);
                gettingMembers = true;
            }

            if (m_OrganizationMembers == null) return string.Empty;
            if (m_OrganizationMembers.TryGetValue(orgId, out var members))
            {
                var member = members.First(x => string.Equals(x.UserId.ToString(), userId));
                if (member != null)
                {
                    return member.Name;
                }
            }
            return string.Empty;
        }
        
        private static async Task GetOrgMembers(OrganizationId orgId)
        {
            if (orgId == OrganizationId.None) return;
            if (m_AllOrganizations != null && m_AllOrganizations.TryGetValue(orgId, out var organization))
            {
                var listMembersAsync = organization.ListMembersAsync(Range.All, CancellationToken.None);
                m_OrganizationMembers ??= new Dictionary<OrganizationId, List<IUserInfo>>();
                await foreach (var member in listMembersAsync)
                {
                    if (!m_OrganizationMembers.ContainsKey(orgId))
                    {
                        m_OrganizationMembers.Add(orgId, new List<IUserInfo>());
                    }
                    m_OrganizationMembers[orgId].Add(member);
                    //m_UserIdToName ??= new Dictionary<string, string>();
                    //m_UserIdToName.TryAdd(member.UserId.ToString(), member.Name);
                }
            }
        }
        
        private static List<IUserInfo> ReturnSuggestedNames(OrganizationId organizationId, string searchName)
        {
            if (m_OrganizationMembers.TryGetValue(organizationId, out var members))
            {
                var ids = m_OrganizationMembers[organizationId];
                if (ids.Count == 0) return null;
                var users = members.Where(x => x.Name.Contains(searchName, StringComparison.OrdinalIgnoreCase));
                var firstTenName = users.Take(Mathf.Min(10, users.Count())).ToList();
                return firstTenName;
            }
            return null;
        }
        
        public static void OnOrganizationsLoaded(List<IOrganization> list)
        {
            gettingMembers = false;
            m_AllOrganizations ??= new Dictionary<OrganizationId, IOrganization>();
            m_AllOrganizations.Clear();
            foreach (var org in list)
            {
                m_AllOrganizations.Add(org.Id, org);
            }
        }
        
        public static void CheckValidInput(TextArea textArea, GridView gridView, VisualElement sendIconButton)
        {
            bool invalid = !CheckTextAreaValidity(textArea);
            var existing = gridView.itemsSource as List<Attachment>;
            bool hasAttachment = existing != null && existing.Count > 0;
            gridView.style.display = hasAttachment? DisplayStyle.Flex: DisplayStyle.None;
            if (NetworkDetector.IsOffline)
            {
                sendIconButton.SetEnabled(false);
                return;
            }
            if (hasAttachment)
            {
                sendIconButton.SetEnabled(true);
            } else if (invalid)
            {
                sendIconButton.SetEnabled(false);
            }
            else
            {
                sendIconButton.SetEnabled(true);
            }
        }

        private static bool CheckTextAreaValidity(TextArea textArea)
        {
            var value = textArea.value;
            bool validity = !(string.IsNullOrEmpty(value) || string.IsNullOrWhiteSpace(value) || value.Length > textArea.maxLength);
            return validity;
        }
        
        public static string ReturnInitials(string nameLabelText)
        {
            if (string.IsNullOrWhiteSpace(nameLabelText))
                return "";

            var parts = nameLabelText.Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
    
            if (parts.Length == 0)
                return "";
    
            if (parts.Length == 1)
                return parts[0][0].ToString().ToUpper();
    
            return (parts[0][0].ToString() + parts[parts.Length - 1][0].ToString()).ToUpper();
        }
        
        public static Color GetRandomBackgroundColorAsUnityColor(string randomColorSeed = null)
        {
            string hexColor = GetRandomBackgroundColor(randomColorSeed);
            if (ColorUtility.TryParseHtmlString(hexColor, out Color color))
            {
                return color;
            }
            return Color.white; // fallback
        }
        
        private static string GetRandomBackgroundColor(string randomColorSeed = null)
        {
            if (string.IsNullOrEmpty(randomColorSeed))
            {
                return Colors[0];
            }

            /*int lastCharIndex = randomColorSeed.Length - 1;
            int lastCharCode = (int)randomColorSeed[lastCharIndex];
            int colorIndex = lastCharCode % Colors.Length;*/
            
            // Use hash of entire string instead of just last character
            int hash = randomColorSeed.GetHashCode();
            int colorIndex = Math.Abs(hash) % Colors.Length;

            return Colors[colorIndex];
        }

        public static void TextAreaRichTextEnable(TextArea textArea)
        {
            var textField = textArea.Q<UnityEngine.UIElements.TextField>();
            textField.Q<TextElement>().enableRichText = Keyboard.current != null;
        }
        
        public static bool IsSupportedImageFormat(string filePath)
        {
            var extension = System.IO.Path.GetExtension(filePath)?.ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => true,
                ".png" => true,
                ".bmp" => true,
                ".tga" => true,
                ".tiff" or ".tif" => true,
                ".gif" => false,  // Unity loads only first frame
                ".webp" => false, // Not natively supported
                ".svg" => false,  // Not supported
                _ => false
            };
        }
    }
}
