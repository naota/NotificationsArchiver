﻿using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Notifications_Archiver
{
	public class MainTabWindow_Archive : MainTabWindow
	{
		private const float listItemSize = 45f; //Verse.Letter.DrawHeight * 1.5

		private const float dateWidth = 100f; //Relatively wide to account for some languages' date strings

		private Logger logger = Current.Game.GetComponent<Logger>();

		private string listFilter;

		private Vector2 scrollPosition;

		//Translation strings
		private string showLettersLabel = "Notifications_Archiver_ShowLetters".Translate();
		private string showMessagesLabel = "Notifications_Archiver_ShowMessages".Translate();
		private string textFilterLabel = "Notifications_Archiver_TextFilter".Translate();
		private string targetedMessageTooltip = "Notifications_Archiver_TargetedMessage_Tooltip".Translate();
		private string dateUnknown = "Notifications_Archiver_Date_None".Translate();

		private string MasterDate(MasterArchive master)
		{
			if (master.dateDayofSeason != -1 && master.dateQuadrum != Quadrum.Undefined && master.dateYear != -1)
			{
				return "Notifications_Archiver_Date_Readout".Translate(new object[]
				{
					master.dateQuadrum.Label(),
					master.dateDayofSeason,
					master.dateYear
				});
			}

			return this.dateUnknown;
		}

		public override Vector2 RequestedTabSize
		{
			get
			{
				return new Vector2(600f, (float)UI.screenHeight * 0.75f);
			}
		}

		public override void PostOpen()
		{
			base.PostOpen();
			this.listFilter = string.Empty;
		}

		public override void DoWindowContents(Rect inRect)
		{
			base.DoWindowContents(inRect);
			GUI.color = Color.white;
			Text.Font = GameFont.Small;

			//Options rects
			float lengthShowLetters = Text.CalcSize(this.showLettersLabel).x;
			float lengthShowMessages = Text.CalcSize(this.showMessagesLabel).x;
			float lengthTextFilterLabel = Text.CalcSize(this.textFilterLabel).x;
			Rect optionsRect = new Rect(inRect.x, inRect.y, inRect.width, Text.LineHeight * 1.2f);
			Rect optionsLetters = new Rect(optionsRect.x, optionsRect.y, lengthShowLetters + 34f, optionsRect.height);
			Rect optionsMessages = new Rect(optionsLetters.xMax + 10f, optionsRect.y, lengthShowMessages + 34f, optionsRect.height);
			Rect optionsFilterLabel = new Rect(optionsMessages.xMax + 10f, optionsRect.y, lengthTextFilterLabel + 10f, optionsRect.height);
			Rect optionsFilterBox = new Rect(optionsFilterLabel.xMax, optionsRect.y, optionsRect.width - optionsLetters.width - optionsMessages.width - optionsFilterLabel.width - 20f, optionsRect.height);

			//Scrolling list - outer rect
			Rect outRect = new Rect(inRect.x, inRect.y + optionsRect.height + 15f, inRect.width, inRect.height - optionsRect.height - 15f);

			//Scrolling list - inner rect
			Rect viewRect = new Rect(outRect.x, outRect.y, outRect.width - 40f, outRect.height);

			//Draw options
			Text.Anchor = TextAnchor.MiddleLeft;
			Widgets.CheckboxLabeled(optionsLetters, this.showLettersLabel, ref logger.ShowLetters);
			Widgets.CheckboxLabeled(optionsMessages, this.showMessagesLabel, ref logger.ShowMessages);
			Widgets.Label(optionsFilterLabel, this.textFilterLabel);
			this.listFilter = Widgets.TextField(optionsFilterBox, this.listFilter);
			Text.Anchor = TextAnchor.UpperLeft; //Reset

			//Draw list
			List<MasterArchive> mastersFiltered = logger.MasterArchives.FindAll(master => MatchesSettings(master));
			viewRect.height = mastersFiltered.Count * listItemSize; //Adjust virtual rect height

			Widgets.BeginScrollView(outRect, ref this.scrollPosition, viewRect, true);

			float curY = viewRect.y;

			for (int i = mastersFiltered.Count - 1; i >= 0; i--)
			{
				var current = mastersFiltered[i];

				Rect currentRect = new Rect(viewRect.x, curY, viewRect.width, listItemSize);

				if (i % 2 == 0)
				{
					Widgets.DrawAltRect(currentRect);
				}

				DrawListItem(currentRect, current);

				curY += listItemSize;
			}

			Widgets.EndScrollView();
		}

		private void DrawListItem(Rect rect, MasterArchive master)
		{
			GUI.color = Color.white;
			Text.Anchor = TextAnchor.MiddleCenter;
			string label = string.Empty;

			//Assign rects
			Rect dateRect = new Rect(rect.x, rect.y, dateWidth, rect.height);
			Rect iconRect = new Rect(dateRect.xMax + 5f, rect.y + 7.5f, Letter.DrawWidth, Letter.DrawHeight);
			Rect labelRect = new Rect(iconRect.xMax + 5f, rect.y, rect.width - dateRect.width - iconRect.width - 10f, rect.height);

			//Draw date info
			Text.Font = GameFont.Tiny;
			Widgets.Label(dateRect, MasterDate(master));
			Text.Font = GameFont.Small; //Reset

			//Letter specific
			if (master.letter != null)
			{
				label = master.letter.label;

				GUI.color = master.letter.def.color;
				GUI.DrawTexture(iconRect, master.letter.def.Icon);
				GUI.color = Color.white; //Reset

				Widgets.DrawHighlightIfMouseover(rect);

				if (master.letter is ChoiceLetter)
				{
					var choiceLetter = master.letter as ChoiceLetter;
					string tooltipText = choiceLetter.text;

					if (tooltipText.Length > 100)
					{
						tooltipText = tooltipText.TrimmedToLength(100) + "...";
					}

					TooltipHandler.TipRegion(rect, tooltipText); //Tooltip with some of the Letter's text for quality of life
				}

				if (Widgets.ButtonInvisible(rect, false))
				{
					master.letter.OpenLetter();
				}
			}

			//Message specific
			if (master.message != null)
			{
				label = master.message.text;

				if (master.message.lookTarget.IsValid)
				{
					GUI.DrawTexture(iconRect, ContentFinder<Texture2D>.Get("UI/Letters/LetterUnopened"));

					Widgets.DrawHighlightIfMouseover(rect);

					TooltipHandler.TipRegion(rect, this.targetedMessageTooltip);

					if (Widgets.ButtonInvisible(rect, false))
					{
						CameraJumper.TryJumpAndSelect(master.message.lookTarget);
					}
				}
			}

			Widgets.Label(labelRect, label);

			Text.Anchor = TextAnchor.UpperLeft; //Reset
		}

		//Filter provider for the scrollable list
		private bool MatchesSettings(MasterArchive m)
		{
			if (logger.ShowLetters && m.letter != null || logger.ShowMessages && m.message != null)
			{
				if (listFilter == string.Empty)
				{
					return true; //Returns true if there is no text filter and the corresponding bool is true
				}

				else if (m.letter != null)
				{
					if (MasterDate(m).IndexOf(listFilter, StringComparison.OrdinalIgnoreCase) >= 0 || m.letter.label.IndexOf(listFilter, StringComparison.OrdinalIgnoreCase) >= 0)
					{
						return true; //Returns true if the text filter matches part of the Letter's date/label
					}

					if (m.letter is ChoiceLetter)
					{
						var choiceLet = m.letter as ChoiceLetter;

						if (choiceLet.text.IndexOf(listFilter, StringComparison.OrdinalIgnoreCase) >= 0)
						{
							return true; //Returns true if the text filter matches part of the Letter's content
						}
					}
				}

				else if (m.message != null)
				{
					if (MasterDate(m).IndexOf(listFilter, StringComparison.OrdinalIgnoreCase) >= 0 || m.message.text.IndexOf(listFilter, StringComparison.OrdinalIgnoreCase) >= 0)
					{
						return true; //Returns true if the text filter matches part of the message's date/text
					}
				}
			}

			return false;
		}
	}
}
