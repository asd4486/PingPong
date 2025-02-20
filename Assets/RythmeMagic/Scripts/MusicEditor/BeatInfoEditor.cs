﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace RythhmMagic.MusicEditor
{
	public enum BeatPiste
	{
		Left,
		Right
	}

	public class BeatInfoEditor : MonoBehaviour
	{
		[SerializeField] EditorBeat beatPrefab;
		[SerializeField] EditorBeatGroup beatGroupPrefab;

		MusicEditorMain main;

		public List<List<EditorBeatGroup>> beatGroupsList { get; private set; }

		[SerializeField] Button[] btnBeatPiste;
		Button selectedBtnPiste;

		private void Awake()
		{
			main = FindObjectOfType<MusicEditorMain>();
		}

		private void Start()
		{
			foreach (var btn in btnBeatPiste)
				btn.onClick.AddListener(() => OnClickSelectPiste(btn));

			OnClickSelectPiste(btnBeatPiste[0]);
		}

		public void Init(List<MusicSheetObject.Beat> list)
		{
			beatGroupsList = new List<List<EditorBeatGroup>>();
			beatGroupsList.Add(new List<EditorBeatGroup>());
			beatGroupsList.Add(new List<EditorBeatGroup>());

			StartCoroutine(InitCoroutine(list));
		}

		IEnumerator InitCoroutine(List<MusicSheetObject.Beat> list)
		{
			foreach (var beatInfos in list)
			{
				for (int i = 0; i < beatInfos.infos.Count; i++)
				{
					var info = beatInfos.infos[i];
					var index = i;

					//change piste if can find beatgroup include this time
					var paralaxGroup = FindBeatGroupByTime(info.posList[0].time, (BeatPiste)index);
					if (paralaxGroup != null) index = (index + 1) % 2;

					var newGroup = CreateBeatGroup(btnBeatPiste[index].transform);

					var groupBeats = new List<EditorBeat>();
					foreach (var posInfo in info.posList)
					{
						var beat = CreateBeat(btnBeatPiste[index].transform);
						beat.Init(posInfo.time, posInfo.pos, newGroup);
						groupBeats.Add(beat);
					}

					newGroup.Init(groupBeats, (BeatPiste)index, info.markerType);
					AddBeatGroup(newGroup);
					yield return null;
				}
			}
		}

		EditorBeat CreateBeat(Transform parent)
		{
			var beat = Instantiate(beatPrefab.gameObject).GetComponent<EditorBeat>();
			beat.transform.SetParent(parent, false);
			beat.transform.localPosition = new Vector3(beat.transform.localPosition.x, beat.transform.localPosition.y, -10);

			beat.onDragEndAction += AdjustBeatInList;
			return beat;
		}

		EditorBeatGroup CreateBeatGroup(Transform parent)
		{
			var newGroup = Instantiate(beatGroupPrefab.gameObject).GetComponent<EditorBeatGroup>();
			newGroup.transform.SetParent(parent, false);

			newGroup.onAddBeatAction += SetBeatMarkerPos;
			newGroup.onRemoveBeatAction += SetBeatMarkerPos;
			newGroup.onDestroyAction += SetBeatMarkerPos;
			newGroup.onDestroyAction += RemoveBeatGroup;
			return newGroup;
		}

		private void SetBeatMarkerPos(EditorBeatGroup obj)
		{
			main.ShowBeatMarkerPos();
		}

		void OnClickSelectPiste(Button piste)
		{
			selectedBtnPiste = piste;
			foreach (var p in btnBeatPiste)
				p.GetComponent<Image>().color = p == selectedBtnPiste ? new Color(1, 1, 0, 0.7f) : new Color(0, 0, 0, 0.8f);
		}

		public void OnClickAddBeatInGroup(float time)
		{
			var piste = GetSelectedPiste();
			if (piste == null || FindBeatByTime(time, piste.Value) != null)
				return;

			var previousBeat = FindClosestBeat(time, piste.Value, false);
			if (previousBeat == null) return;

			var beat = CreateBeat(selectedBtnPiste.transform);
			EditorBeat outBeat;
			var pos = previousBeat.currentGroup.GetTimeCurrentPos(time, out outBeat);
			beat.Init(time, pos, previousBeat.currentGroup);
			previousBeat.currentGroup.AddBeat(beat);
		}

		public void OnClickAddBeat(float time)
		{
			//don't create key when key existed
			var piste = GetSelectedPiste();
			if (piste == null || FindBeatByTime(time, piste.Value) != null) return;

			var beat = CreateBeat(selectedBtnPiste.transform);

			var previousBeat = FindClosestBeat(time, piste.Value, false);
			//auto set beat pos to previous beat pos if existe
			var beatPos = previousBeat != null ? previousBeat.info.pos : Vector2.zero;

			var currentGroup = FindBeatGroupByTime(time, piste.Value);
			if (currentGroup == null)
			{
				currentGroup = CreateBeatGroup(selectedBtnPiste.transform);
				beat.Init(time, beatPos, currentGroup);
				currentGroup.Init(new List<EditorBeat>() { beat }, piste.Value);
				AddBeatGroup(currentGroup);
			}
			else
			{
				beat.Init(time, beatPos, currentGroup);
				currentGroup.AddBeat(beat);
			}
		}

		public void OnClickPasteBeatGroup(BeatGroupInfo groupInfo, float startTime)
		{
			//don't create key when key existed
			var piste = GetSelectedPiste();
			if (piste == null || FindBeatGroupByTime(startTime, piste.Value) != null) return;

			var group = CreateBeatGroup(selectedBtnPiste.transform);
			var beatList = new List<EditorBeat>();
			var oldStartTime = groupInfo.beatInfoList[0].time;
			foreach (var info in groupInfo.beatInfoList)
			{
				var beat = CreateBeat(selectedBtnPiste.transform);
				//set beat time by new start time
				var beatTime = startTime + (info.time - oldStartTime);
				beat.Init(beatTime, info.pos, group);
				beatList.Add(beat);
			}

			group.Init(beatList, piste.Value, groupInfo.markerType);
			AddBeatGroup(group);
		}

		void AdjustBeatInList(EditorBeat _beat)
		{
			var group = _beat.currentGroup;
			if (group == null) return;

			group.AdjustBeatInList(_beat);

			var groups = group.CurrentPiste == BeatPiste.Left ? beatGroupsList[0] : beatGroupsList[1];
			groups.Remove(group);

			//readd group in list
			AddBeatGroup(group);
		}

		void AddBeatGroup(EditorBeatGroup group)
		{
			//top line or bottom line
			var beatGroups = group.CurrentPiste == BeatPiste.Left ? beatGroupsList[0] : beatGroupsList[1];

			if (beatGroups.Count < 1)
			{
				beatGroups.Add(group);
				return;
			}

			var index = 0;
			var groupStartTime = group.beatList[0].info.time;

			for (int i = 0; i < beatGroups.Count; i++)
			{
				if (groupStartTime > beatGroups[i].beatList[0].info.time)
					index += 1;
				else
					break;
			}

			if (index >= beatGroups.Count)
				beatGroups.Add(group);
			else
				beatGroups.Insert(index, group);
		}

		public void OnClickRemoveBeat(float time)
		{
			var piste = GetSelectedPiste();
			if (piste == null) return;

			var beat = FindBeatByTime(time, piste.Value);
			//return when can't find beat
			if (beat != null)
			{
				var group = beat.currentGroup;
				group.RemoveBeat(beat);
				return;
			}

			var beatGroup = FindBeatGroupByTime(time, piste.Value);
			if (beatGroup != null)
				beatGroup.Destroy();

		}

		void RemoveBeatGroup(EditorBeatGroup group)
		{
			//left piste or right piste
			var groupList = group.CurrentPiste == BeatPiste.Left ? beatGroupsList[0] : beatGroupsList[1];
			if (!groupList.Contains(group))
				return;

			groupList.Remove(group);
			main.ShowBeatMarkerPos();
		}

		public void AdjustBeatPos()
		{

			for (int i = 0; i < beatGroupsList.Count; i++)
			{
				//adjust all object
				foreach (var group in beatGroupsList[i])
				{
					foreach (var b in group.beatList)
						b.rectTransfom.anchoredPosition = new Vector2(main.GetPositionByTime(b.info.time), 0);

					//refresh group lenght
					group.SetGroupLenght();
				}
			}
		}

		public EditorBeatGroup FindBeatGroupByTime(float time, BeatPiste piste)
		{
			var groups = piste == BeatPiste.Left ? beatGroupsList[0] : beatGroupsList[1];

			foreach (var group in groups)
			{
				//find group which contains this time
				if (group.beatList.Count > 0 && group.CheckTimeInRange(time))
					return group;
			}
			return null;
		}

		EditorBeat FindBeatByTime(float time, BeatPiste piste)
		{
			var groups = piste == BeatPiste.Left ? beatGroupsList[0] : beatGroupsList[1];

			foreach (var group in groups)
			{
				foreach (var b in group.beatList)
					if (Mathf.Abs(b.info.time - time) < .02) return b;
			}
			return null;
		}

		public EditorBeat FindBeatByTimeInAllPiste(float time)
		{
			for (int i = 0; i < beatGroupsList.Count; i++)
			{
				foreach (var group in beatGroupsList[i])
				{
					foreach (var b in group.beatList)
						if (Mathf.Abs(b.info.time - time) < .02) return b;
				}
			}
			return null;
		}

		public EditorBeat FindClosestBeat(float targetTime, BeatPiste piste, bool findNext)
		{
			//top line or bottom line
			var groups = piste == BeatPiste.Left ? beatGroupsList[0] : beatGroupsList[1];
			var beatList = new List<EditorBeat>();

			foreach (var group in groups)
				foreach (var b in group.beatList)
					beatList.Add(b);

			if (findNext)
				beatList = beatList.Where(b => b.info.time > targetTime).ToList();
			else
				beatList = beatList.Where(b => b.info.time < targetTime).ToList();

			return GetClosestBeat(targetTime, beatList);
		}

		public EditorBeat FindClosestBeatInAllPiste(float targetTime)
		{
			var beatList = new List<EditorBeat>();

			for (int i = 0; i < beatGroupsList.Count; i++)
			{
				foreach (var group in beatGroupsList[i])
					foreach (var b in group.beatList)
						beatList.Add(b);
			}

			return GetClosestBeat(targetTime, beatList);
		}

		EditorBeat GetClosestBeat(float targetTime, List<EditorBeat> beatList)
		{
			EditorBeat closestBeat = null;
			if (beatList.Count < 1) return closestBeat;

			var closestTime = float.MaxValue;
			foreach (var b in beatList)
			{
				var time = Mathf.Abs(b.info.time - targetTime);
				if (time < closestTime)
				{
					closestTime = time;
					closestBeat = b;
				}
			}
			return closestBeat;
		}

		public BeatPiste? GetSelectedPiste()
		{
			if (selectedBtnPiste == null) return null;
			return selectedBtnPiste == btnBeatPiste[0] ? BeatPiste.Left : BeatPiste.Right;
		}
	}
}
