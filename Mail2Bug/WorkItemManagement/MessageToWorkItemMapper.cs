﻿using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using log4net;
using Mail2Bug.Email;
using Mail2Bug.ExceptionClasses;

namespace Mail2Bug.WorkItemManagement
{
    public class MessageToWorkItemMapper
    {
        private readonly string _appendOnlyEmailTitleRegex;
        private readonly string _appendOnlyEmailBodyRegex;
        private readonly SortedList<string, int> _workItemsCache;
        private readonly bool _useConversationGuid;

        /// <summary>
        /// This class is used for mapping incoming messages to work item IDs, either based on the
        /// message contents (title, body), or based on the work items cache, which maps conversation
        /// IDs to work item IDs.
        /// </summary>
        /// <param name="appendOnlyEmailTitleRegex">A regex for retrieving work item ID indication from
        /// a message's title</param>
        /// <param name="appendOnlyEmailBodyRegex">A regex for retrieving work item ID indication from
        /// a message's body text</param>
        /// <param name="workItemsCache">The work items cache, mapping from conversation IDs to work
        /// item IDs</param>
        /// <param name="useConversationGuid">Use conversationID rather than whole conversationIndex
        /// </param>
        public MessageToWorkItemMapper(
            string appendOnlyEmailTitleRegex,
            string appendOnlyEmailBodyRegex,
            SortedList<string,int> workItemsCache,
            bool useConversationGuid)
        {
            _appendOnlyEmailTitleRegex = appendOnlyEmailTitleRegex;
            _appendOnlyEmailBodyRegex = appendOnlyEmailBodyRegex;
            _workItemsCache = workItemsCache;
            _useConversationGuid = useConversationGuid;
    }

        /// <summary>
        /// If a work item already exists for this message, returns its ID. Otherwise, returns null.
        /// </summary>
        /// <param name="message">The email message for which we want to find the work item ID</param>
        /// <returns></returns>
        public int? GetWorkItemId(IIncomingEmailMessage message)
        {
            var appendOnlyId = IsAppendOnlyMessage(message);
            if (appendOnlyId.HasValue)
            {
                Logger.InfoFormat("Append-Only message. Work item ID is {0}", appendOnlyId);
                return appendOnlyId.Value;
            }

            // Just a standard conversation - look up the cache based on the conversation ID (or guid)
            int? workItemId = null;

            // If using only the ConversationID, check for it first
            if (_useConversationGuid)
            {
                workItemId = GetWorkItemIdFromConversationId(message.ConversationGuid, _workItemsCache);
            }

            // So we maintain backwards compatibility, and so we don't lose track of all existing items,
            // fall back to using the whole ConversationIndex
            if (workItemId == null)
            {
                workItemId = GetWorkItemIdFromConversationId(message.ConversationIndex, _workItemsCache);
            }

            return workItemId;
        }

        private int? IsAppendOnlyMessage(IIncomingEmailMessage message)
        {
            var workItemId = GetWorkItemIdFromText(message.Subject, _appendOnlyEmailTitleRegex, "id");

            if (!workItemId.HasValue)
            {
                workItemId = GetWorkItemIdFromText(message.PlainTextBody, _appendOnlyEmailBodyRegex, "id");
            }

            if (!workItemId.HasValue)
            {
                return null;
            }

            return workItemId;
        }

        private static int? GetWorkItemIdFromText(string text, string regex, string capturingGroupName)
        {
            if (string.IsNullOrEmpty(regex))
            {
                return null;
            }

            var regExAppendOnlySearch = new Regex(regex, RegexOptions.IgnoreCase);
            var itemIdMatch = regExAppendOnlySearch.Match(text);

            if (!itemIdMatch.Success)
            {
                return null;
            }

            var workItemIdString = itemIdMatch.Groups[capturingGroupName].Value;

            if (string.IsNullOrEmpty(workItemIdString))
            {
                throw new BadConfigException(
                    "EmailSettings\\AppendOnlyEmailTitleRegex",
                    "Couldn't find \"id\" group when matching title. AppendOnlyEmailTitleRegex must contain a capturing group called 'id'" +
                    " that will contain the TFS item ID to update");
            }

            return Int32.Parse(workItemIdString);
        }

        public static int? GetWorkItemIdFromConversationId(string conversationId, SortedList<string, int> bugs)
        {
            Logger.DebugFormat("GetWorkItemIdFromConversationId {0}", conversationId);
            foreach (var bugConversationId in bugs.Keys)
            {
                if (bugConversationId.Trim() == String.Empty)
                {
                    Logger.DebugFormat("Bug with empty conversation ID found in cache");
                    continue;
                }

                if (conversationId.StartsWith(bugConversationId))
                {
                    return bugs[bugConversationId];
                }

            }

            return null;
        }

        private static readonly ILog Logger = LogManager.GetLogger(typeof(MessageToWorkItemMapper));
    }
}
