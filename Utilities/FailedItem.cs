#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using NINA.Core.Utility;
using NINA.Sequencer;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.Utilities {

    public class FailedItem {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string ParentName { get; set; } = string.Empty;
        public int Attempts { get; set; } = 0;
        public List<FailureReason> Reasons { get; set; } = new List<FailureReason>();

        public static FailedItem FromEntity(ISequenceEntity entity, Exception failureReason) {
            var failedItem = new FailedItem();

            failedItem.Name = entity.Name;
            failedItem.ParentName = entity?.Parent?.Name ?? "";
            if (failedItem is ISequenceItem item) {
                failedItem.Attempts = item.Attempts;
            }
            failedItem.Description = entity.Description;
            failedItem.Category = entity.Category;

            failedItem.Reasons.Add(new FailureReason() { Reason = failureReason.Message });

            if (entity is IValidatable validatableItem && validatableItem.Issues.Count > 0) {
                foreach (var issue in validatableItem.Issues) {
                    if (!string.IsNullOrEmpty(issue)) {
                        var reason = new FailureReason {
                            Reason = issue
                        };

                        failedItem.Reasons.Add(reason);
                    }
                }
            }

            Logger.Debug($"Failed item: {failedItem.Name}, Reason count: {failedItem.Reasons.Count}");

            return failedItem;
        }
    }

    public class FailureReason {
        public string Reason { get; set; } = string.Empty;
    }
}