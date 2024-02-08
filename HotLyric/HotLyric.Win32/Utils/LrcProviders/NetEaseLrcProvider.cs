﻿using HotLyric.Win32.Utils.LyricFiles;
using Kfstorm.LrcParser;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HotLyric.Win32.Utils.LrcProviders
{
    public class NetEaseLrcProvider : ILrcProvider
    {
        public string Name => "NetEase";

        public async Task<Lyric?> GetByIdAsync(string songName, string? artists, object id, CancellationToken cancellationToken)
        {
            if (id is string _id && !string.IsNullOrEmpty(_id))
            {
                try
                {
                    var json = await LrcProviderHelper.TryGetStringAsync($"https://music.163.com/api/song/lyric?id={_id}&lv=-1&kv=1&tv=-1", cancellationToken);
                    if (string.IsNullOrEmpty(json)) return null;

                    var jobj = JObject.Parse(json);
                    var lrcContent = (string?)jobj?["lrc"]?["lyric"];

                    if (string.IsNullOrEmpty(lrcContent)) return null;

                    string? translatedContent = "";
                    try
                    {
                        translatedContent = (string?)jobj?["tlyric"]?["lyric"];
                    }
                    catch (Exception ex)
                    {
                        HotLyric.Win32.Utils.LogHelper.LogError(ex);
                    }

                    return Lyric.CreateClassicLyric(lrcContent!, translatedContent, songName, artists);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    HotLyric.Win32.Utils.LogHelper.LogError(ex);
                }
            }
            return null;
        }

        public async Task<object?> GetIdAsync(string name, string? artists, CancellationToken cancellationToken)
        {
            try
            {
                // 引入 Lyricify 搜索
                var search = await Lyricify.Lyrics.Helpers.SearchHelper.Search(new Lyricify.Lyrics.Models.TrackMultiArtistMetadata()
                {
                    Artists = (artists ?? string.Empty).Split(", ").ToList(),
                    Title = name,
                }, Lyricify.Lyrics.Searchers.Searchers.Netease, Lyricify.Lyrics.Searchers.Helpers.CompareHelper.MatchType.Low);
                if (search is Lyricify.Lyrics.Searchers.NeteaseSearchResult match)
                {
                    return match.Id;
                }

                // 原搜索保留
                var searchKeyword = LrcProviderHelper.BuildSearchKey(name, artists);
                var key = LrcProviderHelper.GetSearchKey(searchKeyword);
                if (string.IsNullOrEmpty(key)) return null;

                var json = await LrcProviderHelper.TryGetStringAsync($"http://music.163.com/api/search/get/web?csrf_token=hlpretag=&hlposttag=&s={Uri.EscapeDataString(searchKeyword)}&type=1&offset=0&total=true&limit=10", cancellationToken);
                if (string.IsNullOrEmpty(json)) return null;

                var jobj = JObject.Parse(json);
                var arr = jobj?["result"]?["songs"] as JArray;
                if (arr?.Count > 0)
                {
                    var musicInfos = arr
                        .Select(c => (
                            id: c.Value<string>("id"),
                            name: c.Value<string>("name"),
                            artists: c.Value<JArray>("artists")
                                ?.Select(x => x.Value<string>("name") ?? string.Empty)
                                ?.Where(x => !string.IsNullOrEmpty(x))
                                ?.ToArray() ?? Array.Empty<string>()))
                        .Where(c => !string.IsNullOrEmpty(c.id) && !string.IsNullOrEmpty(c.name))
                        .Select(c => new LrcProviderHelper.MusicInfomation(c.id, c.name, c.artists))
                        .ToArray();

                    var info = LrcProviderHelper.GetMostSimilarMusicInfomation(key, musicInfos, (int)Math.Ceiling(key.Length / 6d));

                    return info?.Id;
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                HotLyric.Win32.Utils.LogHelper.LogError(ex);
            }

            return null;
        }
    }
}
