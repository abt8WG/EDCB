dofile(mg.document_root..'\\string.lua')
path='Setting\\HttpPublic.ini'
option=0+edcb.GetPrivateProfile('SET','option',false,path)~=0


--EPG情報をTextに変換(EpgTimerUtil.cppから移植)
function _ConvertEpgInfoText2(onid, tsid, sid, eid)
  local s, v = '', edcb.SearchEpg(onid, tsid, sid, eid)
      s=s..'<div id="info">\n<div id="base">\n'
  if v then
      rec=true
      if v.durationSecond then rec=os.time(v.startTime)+v.durationSecond-os.time()>0 and true or false end
    if v.shortInfo then
      s=s..'<h3>'..ConvertTitle(v.shortInfo.event_name)..'</h3>\n'
    end
    s=s..(v.startTime and os.date('<p><span class="date">%Y/%m/%d('..({'日','月','火','水','木','金','土'})[os.date('%w', os.time(v.startTime))+1]..') %H:%M-', os.time(v.startTime))
      ..(v.durationSecond and os.date('%H:%M', os.time(v.startTime)+v.durationSecond) or '未定') or '未定')..'</span>\n'
    for i,w in ipairs(edcb.GetServiceList() or {}) do
      if w.onid==v.onid and w.tsid==v.tsid and w.sid==v.sid then
        s=s..'<span class="service">'..w.service_name..'</span></p>\n'
        break
      end
    end
    if v.shortInfo then
      t=string.gsub(v.shortInfo.text_char, '\r?\n', '<br>\n')
      s=s..'<div>'..string.gsub(t, 'https?://[%w/:%#%$&%?%(%)~%.=%+%-_]+', '<a href="%1" target="_blank">%1</a>')..'</div>\n'
    end
      s=s..'</div>\n'

  s=s..'<div id="detail" class="tab-bar detail">\n'	

    if v.extInfo then
      t=string.gsub(v.extInfo.text_char, '\r?\n', '<br>\n')
      s=s..'<div>\n'..string.gsub(t, 'https?://[%w/:%#%$&%?%(%)~%.=%+%-_]+', '<a href="%1" target="_blank">%1</a>')..'</div>\n'

    end
    s=s..'<ul class="square">\n'
    if v.contentInfoList then
      s=s..'<li>ジャンル\n'
      for i,w in ipairs(v.contentInfoList) do
        s=s..'<p>'..edcb.GetGenreName(math.floor(w.content_nibble/256)*256+255)..' - '..edcb.GetGenreName(w.content_nibble)..'</p>\n'
      end
    s=s..'</li>\n'
    end
    if v.componentInfo then
      s=s..'<li>映像\n<p>'..edcb.GetComponentTypeName(v.componentInfo.stream_content*256+v.componentInfo.component_type)..' '..v.componentInfo.text_char..'</p></li>\n'
    end
    if v.audioInfoList then
      s=s..'<li>音声\n'
      for i,w in ipairs(v.audioInfoList) do
        s=s..'<p>'..edcb.GetComponentTypeName(w.stream_content*256+w.component_type)..' '..w.text_char..'<br>\nサンプリングレート : '
          ..(({[1]='16',[2]='22.05',[3]='24',[5]='32',[6]='44.1',[7]='48'})[w.sampling_rate] or '?')..'kHz</p>\n'
      end
    s=s..'</li>\n'
    end
    s=s..'<li>その他\n<p>'
      ..((v.onid<0x7880 or 0x7FE8<v.onid) and (v.freeCAFlag and '有料放送<br>\n' or '無料放送<br>\n') or '')
      ..string.format('OriginalNetworkID:%d(0x%04X)<br>\n', v.onid, v.onid)
      ..string.format('TransportStreamID:%d(0x%04X)<br>\n', v.tsid, v.tsid)
      ..string.format('ServiceID:%d(0x%04X)<br>\n', v.sid, v.sid)
      ..string.format('EventID:%d(0x%04X)\n', v.eid, v.eid)..'</p>\n'
    s=s..'</li>\n</ul>\n'
  elseif r then
    s=s..'<h3>'..ConvertTitle(r.title)..'</h3>\n'
    s=s..os.date('<p><span class="date">%Y/%m/%d('..({'日','月','火','水','木','金','土'})[os.date('%w', os.time(r.startTime))+1]..') %H:%M-', os.time(r.startTime))
      ..os.date('%H:%M', os.time(r.startTime)+r.durationSecond)..'</span>\n'
      ..'<span class="service">'..r.stationName..'</span></p>\n'
  end
  s=s..'</div>\n'
  return s
end

--録画設定フォームのテンプレート
function RecSettingTemplate(rs)
  local s='<ul><li>録画モード</li>\n<li class="select"><select name="recMode">\n'
    ..'<option value="0"'..(rs.recMode==0 and ' selected' or '')..'>全サービス\n'
    ..'<option value="1"'..(rs.recMode==1 and ' selected' or '')..'>指定サービスのみ\n'
    ..'<option value="2"'..(rs.recMode==2 and ' selected' or '')..'>全サービス（デコード処理なし）\n'
    ..'<option value="3"'..(rs.recMode==3 and ' selected' or '')..'>指定サービスのみ（デコード処理なし）\n'
    ..'<option value="4"'..(rs.recMode==4 and ' selected' or '')..'>視聴\n'
    ..'<option value="5"'..(rs.recMode==5 and ' selected' or '')..'>無効\n</select></li></ul>\n'
    ..'<ul class="sbs"><li>追従</li>\n<li class="onoff sp"><input id="tuijyuuFlag" type="checkbox" name="tuijyuuFlag" value="1"'..(rs.tuijyuuFlag and ' checked="checked"' or '')..'><label class="switch right" for="tuijyuuFlag"></li></ul>\n'
    ..'<ul><li>優先度</li>\n<li class="select"><select name="priority">\n'
    ..'<option value="1"'..(rs.priority==1 and ' selected' or '')..'>1\n'
    ..'<option value="2"'..(rs.priority==2 and ' selected' or '')..'>2\n'
    ..'<option value="3"'..(rs.priority==3 and ' selected' or '')..'>3\n'
    ..'<option value="4"'..(rs.priority==4 and ' selected' or '')..'>4\n'
    ..'<option value="5"'..(rs.priority==5 and ' selected' or '')..'>5\n</select></li></ul>\n'
    ..'<ul class="sbs"><li>ぴったり（？）録画</li>\n<li class="onoff sp"><input id="pittariFlag" type="checkbox" name="pittariFlag" value="1"'..(rs.pittariFlag and ' checked="checked"' or '')..'><label class="switch right" for="pittariFlag"></li></ul>\n'
    ..'<ul class="bottom"><li>録画後動作</li>\n<li class="select sp"><select name="suspendMode">\n'
    ..'<option value="0"'..(rs.suspendMode==0 and ' selected' or '')..'>デフォルト設定を使用\n'
    ..'<option value="1"'..(rs.suspendMode==1 and ' selected' or '')..'>スタンバイ\n'
    ..'<option value="2"'..(rs.suspendMode==2 and ' selected' or '')..'>休止\n'
    ..'<option value="3"'..(rs.suspendMode==3 and ' selected' or '')..'>シャットダウン\n'
    ..'<option value="4"'..(rs.suspendMode==4 and ' selected' or '')..'>何もしない\n</select></li></ul>\n'
    ..'<ul><li></li><li><input id="reboot" type="checkbox" name="rebootFlag" value="1"'..(rs.rebootFlag and ' checked' or '')..'><label for="reboot">復帰後再起動する</label></li></ul>\n'
    ..'<ul class="bottom"><li>録画マージン</li><li class="sp"><input id="usedef" type="checkbox" name="useDefMarginFlag" value="1"'..(rs.startMargin and '' or ' checked')..'><label for="usedef">デフォルト設定で使用</label></li></ul>\n'
    ..'<ul><li></li><li><ul class="compact margin'..(rs.startMargin and '' or ' disabled')..'"><li class="float"><ul class="sbs"><li>開始</li>\n<li class="sp"><input type="number" name="startMargin" value="'..(rs.startMargin or 0)..'"'..(rs.startMargin and '' or ' disabled')..'>秒前</li></ul></li>\n'
    ..'<li class="sp"><ul class="sbs"><li>終了</li>\n<li class="sp"><input type="number" name="endMargin" value="'..(rs.endMargin or 0)..'"'..(rs.startMargin and '' or ' disabled')..'>秒後</li></ul></li></ul></li></ul>\n'
    ..'<ul class="bottom"><li>指定サービス対象データ</li><li class="sp"><input id="smode" type="checkbox" name="serviceMode" value="0"'..(rs.serviceMode%2==0 and ' checked' or '')..'><label for="smode">デフォルト設定で使用</label></li></ul>\n'
    ..'<ul class="bottom"><li></li><li class="sp"><input id="mode1" class="mode"  type="checkbox" name="serviceMode_1" value="0"'..(rs.serviceMode%2~=0 and math.floor(rs.serviceMode/16)%2~=0 and ' checked' or '')..(rs.serviceMode%2==0 and ' disabled' or '')..'><label class="checkbox mode'..(rs.serviceMode%2==0 and ' disabled' or '')..'" for="mode1">字幕データを含める</label></li></ul>\n'
    ..'<ul><li></li><li><input id="mode2" class="mode"  type="checkbox" name="serviceMode_2" value="0"'..(rs.serviceMode%2~=0 and math.floor(rs.serviceMode/32)%2~=0 and ' checked' or '')..(rs.serviceMode%2==0 and ' disabled' or '')..'><label class="checkbox mode'..(rs.serviceMode%2==0 and ' disabled' or '')..'" for="mode2">データカルーセルを含める</label></li></ul>\n'
    ..'<ul><li>連続録画動作</li><li><input id="continue" type="checkbox" name="continueRecFlag" value="1"'..(rs.continueRecFlag and ' checked' or '')..'><label for="continue">後ろの予約を同一ファイルで出力する</label></li></ul>\n'
    ..'<ul><li>使用チューナー強制指定</li>\n<li class="select"><select name="tunerID">\n<option value="0"'..(rs.tunerID==0 and ' selected' or '')..'>自動\n'
  local a=edcb.GetTunerReserveAll()
  for i=1,#a-1 do
    s=s..'<option value="'..a[i].tunerID..'"'..(a[i].tunerID==rs.tunerID and ' selected' or '')..string.format('>ID:%08X(', a[i].tunerID)..a[i].tunerName..')\n'
  end
  s=s..'</select></li></ul>\n'
    ..'<p class="bottom">※プリセットによる変更のみ</p>\n'
    ..'<div id="preset" class="preset"><ul><li>録画後実行bat</li><li id="batFile">'..(rs.batFilePath=='' and '－' or rs.batFilePath )..'</li></ul>\n'
  local b=''
  for i,v in ipairs(rs.recFolderList) do
    recName,recName2,recName3=string.match(v.recNamePlugIn, '^(.+%.dll)(%?(.*))??')
    b=b..'<ul><li>フォルダ</li><li">'..v.recFolder..'</li></ul>\n<ul><li>出力PlugIn</li><li>'..v.writePlugIn..'</li></ul>\n<ul><li>ファイル名PlugIn</li><li>'..(option and recName or v.recNamePlugIn=='' and '－' or v.recNamePlugIn)..'</li></ul>\n'
      ..(option and v.recNamePlugIn~='' and '<ul><li>オプション</li><li><input type="text" name="recName" value="'..(recName3 and recName3 or '')..'" size="25"></li></ul>\n' or '')
      ..'<input type=hidden name="recFolder" value="'..v.recFolder..'"><input type=hidden name="writePlugIn" value="'..v.writePlugIn..'"><input type=hidden name="recNamePlugIn" value="'..(option and recName or v.recNamePlugIn)..'">'
  end
  s=s..(#b>0 and b or '<ul><li>フォルダ</li><li>－</li></ul>\n<ul><li>出力PlugIn</li><li>－</li></ul>\n<ul><li>ファイル名PlugIn</li><li>－</li></ul>')
  s=s..'</div><input type=hidden name="recFolderCount" value="0">\n'
    ..'<ul class="bottom"><li>部分受信サービス</li><li class="sp"><input id="partial" type="checkbox" name="partialRecFlag" value="1"'..(rs.partialRecFlag~=0 and ' checked' or '')..'><label for="partial">別ファイルに同時出力する</label></li></ul>\n'
    ..'<p class="bottom partial"'..(rs.partialRecFlag==0 and ' style="display: none;"' or '')..'>※プリセットによる変更のみ</p>\n'

  b=''
  for i,v in ipairs(rs.partialRecFolder) do
    recName,recName2,recName3=string.match(v.recNamePlugIn, '^(.+%.dll)(%?(.*))??')
    b=b..'<ul><li>フォルダ</li><li>'..v.recFolder..'</li></ul>\n<ul><li>出力PlugIn</li><li>'..v.writePlugIn..'</li></ul>\n<ul><li>ファイル名PlugIn</li><li>'..(option and recName or v.recNamePlugIn=='' and '－' or v.recNamePlugIn)..'</li></ul>\n'
      ..(option and v.recNamePlugIn~='' and '<ul><li>オプション</li><li><input type="text" name="partialrecName" value="'..(recName3 and recName3 or '')..'" size="25"></li></ul>\n' or '')
      ..'<input type=hidden name="partialrecFolder" value="'..v.recFolder..'"><input type=hidden name="partialwritePlugIn" value="'..v.writePlugIn..'"><input type=hidden name="partialrecNamePlugIn" value="'..(option and recName or v.recNamePlugIn)..'">'
  end
  s=s..'<div id="partialpreset"  class="preset partial"'..(rs.partialRecFlag==0 and ' style="display: none;"' or '')..'>\n'..(#b>0 and b or '<ul><li id="partialfolder">フォルダ</li><li>－</li></ul>\n<ul><li>出力PlugIn</li><li id="partialwrite">－</li></ul>\n<ul><li>ファイル名PlugIn</li><li id="partialrecname">－</li></ul>')..'</div>\n'
  s=s..'<input type=hidden name="recFolderCount" value="0">\n'
  return s
end

--メニューのテンプレート
function menu(title)
  local s=[=[
<div id="menu">
<div id="menu_ico"><i class="material-icons">menu</i></div>
<div class="menu">
]=]..(title and '<span id="pagetitle">'..title..'</span>\n' or '')..[=[
<div class="space"></div>
<div id="textfield">
<label id="search-lab" class="search-ico" for="andKey"><i class="material-icons">search</i></label>
<div class="expandable">
<form id="search-bar" method="GET" action="search.html"><input id="andKey" class="input" type="text" name="andKey"></form>
</div>
</div>
<nav>
<a href="epg.html">番組表</a>
<a href="reserve.html">予約一覧</a>
<a href="autoaddepg.html">EPG予約</a>
<a href="recinfo.html">録画結果</a>
</nav>
</div>
</div>
]=]
  return s
end

--サイドメニューのテンプレート
function sidemenu(add)
  local s=[=[
<div id="sidemenu">
<div id="title">
<a class="title"  href="index.html" tabindex="1">EpgTimer</a>
</div>
<nav>
]=]..(add and add or '')..[=[
<a class="link" href="epg.html">番組表</a>
<a class="link" href="epgweek.html">週間番組表</a>
<a class="link" href="reserve.html">予約一覧</a>
<a class="link" href="tunerreserve.html">チューナー別</a>
<a class="link" href="autoaddepg.html">EPG予約</a>
<a class="link" href="recinfo.html">録画結果</a>
<a class="link" href="search.html">検索</a>
<a class="link" href="setting.html">設定</a>
</nav>
</div>
<div id="obfuscator"></div>
<div id="obfuscator2"></div>
<div id="dialog" class="dialog"><div class="card">
<div class="messege"><div></div></div>
<div class="button-wrap footer"></div>
</div><div class="obfuscator"></div></div>
]=]
  return s
end
