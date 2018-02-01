﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Newtonsoft.Json.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Notifications_Archiver
{
	public class Archiver : GameComponent
	{
		private List<MasterArchive> archives = new List<MasterArchive>();

		private Queue<Action> queuedCleanup = new Queue<Action>();

		private Dictionary<MasterArchive, bool> currentlyQueued = new Dictionary<MasterArchive, bool>();

		private Action currentCleanup = null;

		public int ticksSinceArchiveValidation = 0;

		public bool EnableArchiving = true;

		public bool ShowLetters = true;

		public bool ShowMessages = true;

		public List<MasterArchive> MasterArchives => this.archives;

		public void ValidateArchiveTarget(MasterArchive archive)
		{
			Predicate<GlobalTargetInfo> invalidTarget = target => !target.IsValid || target.ThingDestroyed || (target.HasThing && target.Thing.MapHeld == null);

			Letter letter = archive.letter;

			if (letter != null)
			{
				if (invalidTarget(letter.lookTarget))
				{
					letter.lookTarget = GlobalTargetInfo.Invalid;
				}
			}

			else if (invalidTarget(archive.lookTarget))
			{
				archive.lookTarget = GlobalTargetInfo.Invalid;
			}

			this.currentlyQueued[archive] = false;
			this.ticksSinceArchiveValidation = 0;
			this.currentCleanup = null;
		}

		public void NewArchive(Letter letter, string text, GlobalTargetInfo target)
		{
			if (this.EnableArchiving)
			{
				MasterArchive newArchive;

				if (letter != null)
				{
					newArchive = new MasterArchive(letter);

					//Dummify complex letters to avoid players exploiting the archiver		
					if (letter is ChoiceLetter && letter.GetType() != typeof(StandardLetter))
					{
						ChoiceLetter choiceLet = newArchive.letter as ChoiceLetter;

						newArchive.letter = new DummyStandardLetter
						{
							def = choiceLet.def,
							label = choiceLet.label,
							lookTarget = choiceLet.lookTarget,
							disappearAtTick = -1,
							title = choiceLet.title,
							text = choiceLet.text
						};
					}
				}

				else
				{
					newArchive = new MasterArchive(text, target);
				}

				if (Controller.PostSlack && Controller.SlackURL != "" && Controller.SlackChannel != "") {
					ServicePointManager.ServerCertificateValidationCallback = CertificateValidationCallback;
					var wc = new WebClient();
					JObject msg = new JObject(
							new JProperty("text", newArchive.Text),
							new JProperty("username", "RimWorld Notify Bot"),
							new JProperty("channel", Controller.SlackChannel.Value));
					wc.Headers.Add(HttpRequestHeader.ContentType, "application/json;charset=UTF-8");
					wc.Encoding=Encoding.UTF8;
					string cont = msg.ToString();
					Log.Message(cont);
					wc.UploadString(Controller.SlackURL, cont);
				}

				this.archives.Add(newArchive);

				MainTabWindow_Archive.mustRecacheList = true;
			}
		}

		private bool CertificateValidationCallback(Object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) {
			return true;
		}

		public void QueueArchiveCleanup(MasterArchive master)
		{
			this.currentlyQueued.TryGetValue(master, out bool queued);

			if (!queued)
			{
				this.queuedCleanup.Enqueue(delegate
				{
					this.ValidateArchiveTarget(master);
				});

				this.currentlyQueued[master] = true;
			}
		}

		public override void GameComponentUpdate()
		{
			base.GameComponentUpdate();

			this.currentCleanup?.Invoke();

			if (this.currentCleanup == null && this.queuedCleanup.Count > 0)
			{
				this.currentCleanup = this.queuedCleanup.Dequeue();
			}
		}

		public override void GameComponentTick()
		{
			base.GameComponentTick();

			if (++this.ticksSinceArchiveValidation == GenDate.TicksPerDay)
			{
				this.ticksSinceArchiveValidation = 0;

				for (int i = 0; i < this.archives.Count; i++)
				{
					this.QueueArchiveCleanup(this.archives[i]);
				}
			}
		}

		public override void ExposeData()
		{
			Scribe_Collections.Look(ref this.archives, "archives", LookMode.Deep);
			Scribe_Values.Look(ref this.EnableArchiving, "EnableArchiving", true);
			Scribe_Values.Look(ref this.ShowLetters, "ShowLetters", true);
			Scribe_Values.Look(ref this.ShowMessages, "ShowMessages", true);
		}

		public Archiver()
		{
		}

		public Archiver(Game game)
		{
		}
	}
}
