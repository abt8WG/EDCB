//ドラッグスクロール
(function() {
  $.fn.dragScroll = function() {
    var target = this;
    $(this).mousedown(function (event) {
      if (event.which==1){
        $(this)
          .data('down', true)
          .data('x', event.clientX)
          .data('y', event.clientY)
          .data('scrollLeft', this.scrollLeft)
          .data('scrollTop', this.scrollTop);
        return false;
      }
    });
    // ウィンドウから外れてもイベント実行
    $(document).mousemove(function (event) {
      if ($(target).data('down') == true && event.which==1) {
        $("#wrap").css({'cursor': 'move'})
        // スクロール
        target.scrollLeft($(target).data('scrollLeft') + ($(target).data('x') - event.clientX)*1.6);
        target.scrollTop($(target).data('scrollTop') + ($(target).data('y') - event.clientY)*1.6);
        return false; // 文字列選択を抑止
      }
    }).mouseup(function (event) {
      if (event.which==1){
        $(target).data('down', false);
        $("#wrap").css({'cursor': 'default'});
      }
    });
    return this;
  }
})(jQuery);
//http://moriwaki.net/cms/?p=503から一部改変しました


if ((navigator.userAgent.indexOf('iPhone') > 0 &&
     navigator.userAgent.indexOf('iPad') == -1) ||
     navigator.userAgent.indexOf('iPod') > 0 ||
     navigator.userAgent.indexOf('Android') > 0){
	mobile=true
	document.write('<link href="css/sp.css" rel="stylesheet" type="text/css">');
}else{
	mobile=false
	document.write('<link href="css/pc.css" rel="stylesheet" type="text/css">');
}

function time(){
	date=new Date().getDate();
	hour=new Date().getHours();
	min =new Date().getMinutes();
	sec =new Date().getSeconds();
	HOUR=hour;
	MIN=min;
	if(date<10){date="0"+ date;}
	if(hour<10){hour="0"+ hour;}
	if(min<10){min="0"+ min;}
	if(sec<10){sec="0"+ sec;}
}

function rec(){		//予約一覧
	time()
	var recstart=$(".recstart_"+date+"-"+hour+"-"+min+"-"+sec);
	var recend=$(".recend_"+date+"-"+hour+"-"+min+"-"+sec);
	recstart.removeClass("switch").addClass("rec");
	if (recstart.hasClass("on")){
		recstart.removeClass("on").addClass("recording").data("reserve",0);
	}else{
		recstart.addClass("not");
	}
	recend.children().fadeOut(1000,function(){
		recend.slideUp(1000,function(){
			recend.remove();
		});
	});
}

function tab(tab){	//タブ切替
	$("#tab-bar label").removeClass("active");
	$(tab).addClass("active").scrollLeft(0);
	$(".tab-bar").hide();
	$("."+$(tab).data("val")).show();
	$("#main").scrollTop(0);
	if ($(tab).data("val")=='movie-tab' && !load){
		$('video').load();
		load=true;
	}
}

function dialog(messege,button,val){				//dialog(メッセージ,クラス名,ボタン表示文字):ダイアログ表示
	$("#dialog .button").remove();
	$("#dialog .messege>div").html(messege);
	if(button==undefined){					//第2引数なしでボタン非表示
		$("#dialog .footer").hide();
	}else if(button===true){					//第2引数:trueで閉じるボタン表示
		$("#dialog .footer").append('<input class="button close" type="button" value="閉じる">');
	}else{
		$("#dialog .footer").append('<input class="button '+button+'" type="button" value="'+val+'">')
	}
	$("#dialog .card").addClass("visible");			//ダイアログ表示
}

$(function(){
	$(document).on("click",".close",function(){		//ダイアログを閉じる
		$("#dialog .card").removeClass("visible");
	});
	$(document).on("click",".reload.button",function(){
		location.reload();
	});
	$(document).on("click",".back",function(){		//ページを戻る
		history.back();
	});

	$('#obfuscator2').hammer().on("swiperight", function(){	//サイドメニュー表示
		$("#sidemenu").addClass("visible");
	});
	$('#obfuscator').hammer().on("swipeleft", function(){	//サイドメニュー非表示
		$("#sidemenu").removeClass("visible");
	});

	if (mobile==true) {	//モバイルだけ有効に
		$(".swipe").addClass("swiper");
		swipe=true
	}
	$('.swiper').hammer().on("swipeleft", function() {		//スワイプでタブ切替
		if(!$("#tab-bar label.active").is(":last-child") && swipe==true){
			tab($("#tab-bar label.active").next());
			$("#tab-bar").animate({scrollLeft:($("#tab-bar label.active").width()+30)*$("#tab-bar label.active").prevAll().length}, 200, "linear");
		}
	});
	$('.swiper').hammer().on("swiperight", function() {
		if(!$("#tab-bar label.active").is(":first-child") && swipe==true){
			tab($("#tab-bar label.active").prev());
			$("#tab-bar").animate({scrollLeft:($("#tab-bar label.active").width()+30)*$("#tab-bar label.active").prevAll().length}, 200, "linear");
		}
	});
	$('.swiper').css("-webkit-user-select", "text");	//文字選択できるように

	if (mobile==false) {
		$(".drag").dragScroll();				//ドラッグスクロール
	}

	$("#menu_ico").click(function(){			//サイドメニュー
		$("#sidemenu").addClass("visible");
	});
	$("#obfuscator").click(function(){
		$("#sidemenu").removeClass("visible");
	});

	if($("#advanced").prop('checked')) {			//詳細検索
		$(".advanced").show();
		$(".hide").hide();
	}
	$("#advanced").change(function(){
		if($(this).prop('checked')) {
			$(".advanced").show();
			$(".hide").hide();
		}else{
			$(".advanced").hide();
			$(".hide").show();
		}
	});

	$("#andKey").focus(function(){				//検索バー
		$("#textfield").addClass("focused");
		$("#pagetitle").addClass("focused");
	});
	$("#andKey").blur(function(){
		if($(this).val() == ""){
			$("#textfield").removeClass("focused");
			$("#pagetitle").removeClass("focused");
		}
	});
	$("#search-lab").click(function(){
		if($("#andKey").val() != ""){
			$("#search-bar").submit();
		}else{
			$("#textfield").removeClass("focused");
		}
	});
	if(!$("#andKey").val() == ""){	//戻った時用
		$("#textfield").addClass("focused");
		$("#pagetitle").addClass("focused");
	}


	$("#main").scroll(function () {				//検索キーワード表示
		if($("#main").scrollTop()>0){
			$("#sub-search").css("margin-top",0);			
			$("#main").css("margin-top",-$("#sub-search").height());
			$("#loading").css("margin-top",$("#sub-search").height());
		}else{
			$("#sub-search").css("margin-top",-$("#sub-search").height());
			$("#main").css("margin-top",0);
			$("#loading").css("margin-top",0);
		}
	});
	$("#saerch-andKey").val($("#search .key").val());		//キーワード同期
	$(".key").change(function(){	
		$(".key").val($(this).val());
	});
	$("#sub-search-bar").click(function(){
		if($(".key").val() != ""){
			$("#search").submit();
		}
	});
	
	$('table:not("#search-list") tr[data-href]').click(function(e){		//一覧の行をリンクに
		if(!$(e.target).is('.flag,.flag span,.count li')){
			window.location = $(e.target).closest('tr').data('href');
		};
	});
	$(".search [data-href]").click(function(e){
		if(!$(e.target).is('.flag,.flag span,.count li')){
			if($("#advanced").prop('checked')) {
				$("#hidden").append($("<input>",{type:"hidden",name:"advanced",value:"1"}));
			}
			$("#hidden").attr("action", $(this).data("href")).submit();
		};
	});
	$('.count[data-href]').click(function(){		//登録数クリックで検索
		window.location = $(this).data('href');
	});

	$("#tab-bar label").click(function(){			//タブ表示切替
		tab(this);
	});

	$('#usedef').change(function(){			//録画マージン
		if($(this).prop('checked')) {
			$(".margin input").prop('disabled', true);
			$(".margin").addClass("disabled");
		}else{
			$(".margin input").prop('disabled', false);
			$(".margin").removeClass("disabled");
		}
	});
	$('#smode').change(function(){			//指定サービス対象データ
		if ($(this).prop('checked')) {
			$(".mode").prop('disabled', true);
			$(".mode").addClass("disabled");
		}else{
			$(".mode").prop('disabled', false);
			$(".mode").removeClass("disabled");
		}
	});

	$('#partial').change(function(){		//部分受信同時出力で詳細表示
		if($(this).prop('checked')) {
			$(".partial").show();
		}else{
			$(".partial").hide();
		}
	});

	$('#content').change(function(){		//大分類ジャンル選択
		var content=$('#content').val();
                $('#content2 option').show();						//一度すべて表示
		if (content!="all"){									//選択ジャンル以外の中分類を
                	$('#content2 option').not('.g'+content).hide();		//非表示
			$('#content2 option').not('.g'+content).prop('selected', false);		//非選択
			subGenre=$('#subGenre').prop("checked");
			$('#subGenre').prop('checked',true).prop("disabled", true);
			$('[for=subGenre]').addClass("disabled");
		}else{
			$('#subGenre').prop("disabled", false).prop("checked",subGenre);
			$('[for=subGenre]').removeClass("disabled");
			if(subGenre==false){$(".subGenre").hide();}
		}
	});
	$('.g_celar').click(function(){			//全ジャンル選択解除
		$('#content2 option').prop('selected', false);
	});
	$('#subGenre').change(function(){
		if ($(this).prop('checked')){
			$(".subGenre").show();
		}else{
			$(".subGenre").hide();
		}
	});
	if (!$('#subGenre').prop('checked')){
			$(".subGenre").hide();
	};

	$('#service').change(function(){		//ネットワーク選択
		var content=$('#service').val();
                $('#service2 option').show();		//一度すべて表示
		if(content!="all"){								//選択ネットワーク以外のサービスを
                	$('#service2 option').not('.'+content).hide();				//非表示
			$('#service2 option').not('.'+content).prop('selected', false);	//非選択
		}
	});

	$("[name=dayList]").change(function(){		//時間絞り、曜日設定切替
		if ($("#dayList").prop('checked')){
			$("#between").removeClass("disabled");
			$("#between select").prop('disabled', false);
		}else{
			$("#between").addClass("disabled");
			$("#between select").prop('disabled', true);
		}
		if ($("#dayList_").prop('checked')){
			$("#each").removeClass("disabled");
			$("#each input").prop('disabled', false);
		}else{
			$("#each").addClass("disabled");
			$("#each input").prop('disabled', true);
		}
	});
	$(document).on("click","#dialogeditor .disabled",function(){
		$(this).prev().children("input").prop('checked', true);
		if ($("#dayList").prop('checked')){
			$("#between").removeClass("disabled");
			$("#between select").prop('disabled', false);
		}else{
			$("#between").addClass("disabled");
			$("#between select").prop('disabled', true);
		}
		if ($("#dayList_").prop('checked')){
			$("#each").removeClass("disabled");
			$("#each input").prop('disabled', false);
		}else{
			$("#each").addClass("disabled");
			$("#each input").prop('disabled', true);
		}
	});
	$("#startHour").change(function(){		//時間連動
		var val=$("#startHour").val()*1+$("#startHour").data("margin");
		if(val>23){val=val-24;}
		if(val<0){val=val+24;}
		if(val<10){val="0"+val;}
		$("#endHour option[value="+val+"]").prop('selected', true);
	});
	$("#endHour").change(function(){		//時間差保存
		$("#startHour").data("margin",$("#endHour").val()*1-$("#startHour").val()*1);
	});
	function add_time(){		//時間絞り込み追加
			$("#dateList_select").append('<option value="'+startDayOfWeek+'-'+startHour+':'+startMin+'-'+endDayOfWeek+'-'+endHour+':'+endMin+'">'+startDayOfWeek+' '+startHour+':'+startMin+' ～ '+endDayOfWeek+' '+endHour+':'+endMin+'</otion>');
			$("#dateList_SP").append('<div class="card" data-count="'+$("#dateList_SP .card").length+'"><p>'+startDayOfWeek+'</p><p>'+startHour+':'+startMin+'</p><p>～</p><p>'+endDayOfWeek+'</p><p>'+endHour+':'+endMin+'</p><div>');
			var val=startDayOfWeek+'-'+startHour+':'+startMin+'-'+endDayOfWeek+'-'+endHour+':'+endMin;
			if($("[name=dateList]").val()!=""){val=","+val;}
			$("[name=dateList]").val($("[name=dateList]").val()+val);
	}
	$(".add_time").click(function(){		//時間絞り込み追加
		startDayOfWeek = $("#startDayOfWeek").val();
		startHour = $("#startHour").val();
		startMin = $("#startMin").val();
		endDayOfWeek = $("#endDayOfWeek").val();
		endHour = $("#endHour").val();
		endMin = $("#endMin").val();
		if ($("#dayList").prop('checked')){
			add_time();
		}else{
			$.each($(".DayOfWeek:checked"),function(){
				startDayOfWeek = $(this).val();
				endDayOfWeek = $(this).val();
				add_time();
			})
		}
	});
	$(document).on("click","#dialoglist .card",function(){		//時間絞り込み選択
		var option=$("#dateList_select option").eq($(this).data("count"));
		if(option.prop('selected')==true){
			option.prop('selected', false);
			$(this).removeClass("selected");
		}else{
			option.prop('selected', true);
			$(this).addClass("selected");
		}
	});
	$(".del_time").click(function(){		//時間絞り込み削除
		$("#dateList_select option:selected").remove();
		$("[name=dateList]").val("");
		$("#dateList_SP").empty();
		$.each($("#dateList_select option"),function(i){
			var val=$(this).val();
			var rep=val.match(/^(.)\-(\d+):(\d+)\-(.)\-(\d+):(\d+)$/);
			if($("[name=dateList]").val()!=""){val=","+val;}
			$("[name=dateList]").val($("[name=dateList]").val()+val);
			$("#dateList_SP").append('<div class="card" data-count="'+i+'"><p>'+rep[1]+'</p><p>'+rep[2]+':'+rep[3]+'</p><p>～</p><p>'+rep[4]+'</p><p>'+rep[5]+':'+rep[6]+'</p><div>');
		})
	});
	$("#edit_time").click(function(){		//編集画面表示
		$("#editor").addClass("visible");
		$("#dateList_edit>div").addClass("dialog");
		if (mobile==true){
			swipe=false
			$("#dateList_main #dateList_SP").appendTo("#dialoglist");
		}else{
			$("#editor").addClass("card").children(".editor").addClass("messege");
			$("#dateList_main #dateList_select").appendTo("#dialogselect");
		}
	});
	$(".close_time").click(function(){		//編集画面非表示
		$("#editor").removeClass("visible");
		$("#dateList_edit>div").removeClass("dialog");
		if (mobile==true){
			swipe=true
			$("#dialoglist #dateList_SP").prependTo("#dateList_main .dateList_SP");
		}else{
			$("#editor").removeClass("card").children(".editor").removeClass("messege");
			$("#dialogselect #dateList_select").appendTo("#dateList_main .multiple");
		}
	});
	if (mobile==true) {
			$("#editor").addClass("card").children(".editor").addClass("messege");
	}

	$('#image').change(function(){			//EPG予約ネットワーク抽出
		if (!$(this).prop('checked')){
			if ($('#DTV').prop('checked')){
				$("#service2 option.DTV").removeClass("hide");
			}
			if ($('#SEG').prop('checked')){
				$("#service2 option.SEG").removeClass("hide");
			}
			if ($('#BS').prop('checked')){
				$("#service2 option.BS").removeClass("hide");
			}
			if ($('#CS').prop('checked')){
				$("#service2 option.CS").removeClass("hide");
			}
			if ($('#HD').prop('checked')){
				$("#service2 option.HD").removeClass("hide");
			}
			if ($('#S-other').prop('checked')){
				$("#service2 option.S-other").removeClass("hide");
			}
		}else{
			$("#service2 option:not(.image)").addClass("hide");
			$('#service2 option:not(.image)').prop('selected', false);
		}
	});
	$('.network').change(function(){		//検索ネットワーク抽出
		var idname = $(this).attr("id");
		if (!$(this).prop('checked')){
			$('#service2 option.'+idname).addClass("hide");
			$('#service2 option.'+idname).prop('selected', false);
		}else if ($('#image').prop('checked')){
			$('#service2 option.image.'+idname).removeClass("hide");
		}else{
			$('#service2 option.'+idname).removeClass("hide");
		}
	});

	$('#s_all').click(function(){			//表示ネットワークのサービス選択
		var content=$('#service').val();
		if(content=="all"){
		$('#service2 option').prop('selected', true);
		}else{
		$('#service2 .'+content).prop('selected', true);
		}
	});

	$('#select').click(function(){			//表示ネットワークのサービス選択
		$('#service2 option:not(.hide)').prop('selected', true);
	});

	$('span.add').click(function(){			//検索でEPG予約追加
		$('#hidden').attr('action','autoaddepginfo.html'+$(this).data('id')).submit();
	});
	
	$(document).ajaxError(function(e,xhr, textStatus, errorThrown){		//通信エラー
		dialog("Error "+xhr.status+": "+xhr.statusText,true);
	});
	$(document).ajaxStart(function(){$("#loading>div").addClass("is-active");});		//通信開始 グルグル表示
	$(document).ajaxComplete(function(){$("#loading>div").removeClass("is-active");});	//通信終了 グルグル非表示

	$(".flag span").click(function(){		//予約一覧等 有効/無効切替
		var reserve=$(this);
		var url, data;
		if (reserve.data("reserve")>0){						//reserveidで
			url="api/reservetoggle";
			data={"id":reserve.data("reserve")};
		}else if (reserve.data("eid")>0){					//onid,tsid,sid,eidで
			url="api/oneclickadd";
			data={	"onid":reserve.data("onid"),
				"tsid":reserve.data("tsid"),
				"sid":reserve.data("sid"),
				"eid":reserve.data("eid") };
		}
		$.ajax({
			url: url,
			data: data,

			success: function(result, textStatus, xhr) {
				var xml=$(xhr.responseXML);
				if(xml.find('success').length>0){
					var start=xml.find('start').text();
					var recmode=xml.find('recmode').text();
					var overlapmode=xml.find('overlapmode').text();
					var id=xml.find('reserveid').text();
					//親のtrのclass設定
					reserve.removeClass("addrec");
					reserve.parents("tr").removeClass("disablerec partially shortage");
					if(recmode==5 && !reserve.parents("table").is("#search-list")){	//無効時
						reserve.parents("tr").addClass("disablerec");
					}else if(overlapmode==1){//チューナー不足
						reserve.parents("tr").addClass("partially");
					}else if(overlapmode==2){//一部録画
						reserve.parents("tr").addClass("shortage");
					}
					
					if (start==1){//番組開始済み
						reserve.removeClass("switch").addClass("rec");	//録画アイコン追加
						if (recmode==5){//無効時
							reserve.addClass("not").data("reserve", id);
						}else{
							reserve.addClass("recording").removeClass("not").data("reserve", 0).data("eid", 0);	//誤爆して無効にしないようにid,eidを0に
						}
					}else{
						reserve.removeClass("rec").addClass("switch");	//スイッチアイコン追加
						if(id){reserve.data("reserve", id);}
						if (recmode==5){
							reserve.removeClass("on");			//無効
						}else{
							reserve.addClass("on");			//有効
						}
					}
				}else{
					dialog("Error: "+$(xml).find('err').text(),true);
				}
			}
		})
	});

	$("[name=presetID]").change(function(){		//プリセット読み込み
		var id ,url;
 		var tag='recpresetinfo';
 		var tagid='id';
		if ($(this).val()!=65535){
			url="api/EnumRecPreset";
		        id=$(this).val();
		}else{						//予約時
			if($("[name=id]").length>0){
				url="api/EnumReserveInfo";
				id=$("[name=id]").val();
				tag='reserveinfo'
				tagid='ID'
			}else{
				url="api/EnumAutoAdd";	
				id=$("#reserveadd").data("autoid");
			}
		}
		$.ajax({
			url: url,

			success: function(result, textStatus, xhr) {
				var xml=$(xhr.responseXML);
				var preset;
				xml.find(tag).each(function(){
					if($(this).find(tagid).text()==id){
						preset=true;
						$("[name=recMode]").val(recMode=$(this).find('recMode').text());	//録画モード

						if($(this).find('tuijyuuFlag').text()==1){				//追従
							$("[name=tuijyuuFlag]").prop('checked',true);
						}else{
							$("[name=tuijyuuFlag]").prop('checked',false);
						}

						$("[name=priority]").val($(this).find('priority').text());		//優先度

						if($(this).find('pittariFlag').text()==1){				//ぴったり
							$("[name=pittariFlag]").prop('checked',true);
						}else{
							$("[name=pittariFlag]").prop('checked',false);
						}

						$("[name=suspendMode]").val($(this).find('suspendMode').text());	//録画後動作

						if($(this).find('rebootFlag').text()==1){				//復帰後
							$("[name=rebootFlag]").prop('checked',true);
						}else{
							$("[name=rebootFlag]").prop('checked',false);
						}

						if($(this).find('useMargineFlag').text()==0){				//録画マージン
							$("[name=useDefMarginFlag]").prop('checked',true);
							$(".margin input").prop('disabled', true);
							$(".margin").addClass("disabled");
						}else{
							$("[name=useDefMarginFlag]").prop('checked',false);
							$(".margin input").prop('disabled', false);
							$(".margin").removeClass("disabled");
						}

						$("[name=startMargin]").val($(this).find('startMargine').text());	//開始
						$("[name=endMargin]").val($(this).find('endMargine').text());		//終了

						if(($(this).find('serviceMode').text()%2)==0){				//指定サービス
							$("[name=serviceMode]").prop('checked',true);
							$(".mode").prop('disabled', true);
							$(".mode").addClass("disabled");
							$("[name=serviceMode_1]").prop('checked',false);
							$("[name=serviceMode_2]").prop('checked',false);
						}else{
							$("[name=serviceMode]").prop('checked',false);
							$(".mode").prop('disabled', false);
							$(".mode").removeClass("disabled");
							if((Math.floor($(this).find('serviceMode').text()/16)%2)!=0){	//字幕
								$("[name=serviceMode_1]").prop('checked',true);
							}else{
								$("[name=serviceMode_1]").prop('checked',false);
							}
							if((Math.floor($(this).find('serviceMode').text()/32)%2)!=0){	//データ
								$("[name=serviceMode_2]").prop('checked',true);
							}else{
								$("[name=serviceMode_2]").prop('checked',false);
							}
						}

						if($(this).find('continueRecFlag').text()==1){				//連続録画動作
							$("[name=continueRecFlag]").prop('checked',true);
						}else{
							$("[name=continueRecFlag]").prop('checked',false);
						}

						$("[name=tunerID]").val($(this).find('tunerID').text());		//チューナー
						
						var batfile;
						if($(this).find('batFilePath').text().length>0){			//bat
							batfile=$(this).find('batFilePath').text();
						}else{
							batfile='－'
						}
						$("#preset").empty().append('<ul><li>録画後実行bat</li><li id="batFile">' + batfile + '</li></ul>\n');
						
						if($(this).find('recFolderList').text().length>0){			//プリセット
							$(this).find('recFolderList').find('recFolderInfo').each(function(){
								var folderval=$(this).find('recFolder').text();
								var writeval=$(this).find('writePlugIn').text();
								var recnameval=$(this).find('recNamePlugIn').text();
								var folder; var write; var recname; var op;

								if($(this).find('recFolder').text().length>0){
									folder=$(this).find('recFolder').text();
								}else{
									folder='－';
								}

								if($(this).find('writePlugIn').text().length>0){
									write=$(this).find('writePlugIn').text();
								}else{
									write='－';
								}

								if($(this).find('recNamePlugIn').text().length>0){
									if($(this).find('recNamePlugIndll').text().length>0){
										recname=$(this).find('recNamePlugIndll').text();
										recnameval=$(this).find('recNamePlugIndll').text();
										op='<ul><li>オプション</li><li><input type="text" name="recName" value="'+$(this).find('recNamePlugInoption').text()+'" size="25"></li></ul>';
									}else{
										recname=$(this).find('recNamePlugIn').text();
									}
								}else{
									recname='－';
								}
								$("#preset").append('<input type=hidden name="recFolder" value="' + folderval + '"><input type=hidden name="writePlugIn" value="' + writeval + '"><input type=hidden name="recNamePlugIn" value="' + recnameval + '">' +
								'<ul><li>フォルダ</li><li">' + folder + '</li></ul><ul><li>出力PlugIn</li><li>' + write + '</li></ul><ul><li>ファイル名PlugIn</li><li>' + recname + '</li></ul>').append(op);
							})
						}else{
							$("#preset").append('<ul><li>フォルダ</li><li">－</li></ul><ul><li>出力PlugIn</li><li>－</li></ul><ul><li>ファイル名PlugIn</li><li>－</li></ul>');
						}

						if($(this).find('partialRecFlag').text()==1){				//部分受信
							$("#partial").prop('checked',true);
							$(".partial").show();
						}else{
							$("#partial").prop('checked',false);
							$(".partial").hide();
						}

						$("#partialpreset").empty();						//部分受信プリセット
						if($(this).find('partialRecFolder').text().length>0){
							$(this).find('partialRecFolder').find('recFolderInfo').each(function(){
								var folderval=$(this).find('recFolder').text();
								var writeval=$(this).find('writePlugIn').text();
								var recnameval=$(this).find('recNamePlugIn').text();
								var folder; var write; var recname; var op;
								if($(this).find('recFolder').text().length>0){
									folder=$(this).find('recFolder').text();
								}else{
									folder='－';
								}

								if($(this).find('writePlugIn').text().length>0){
									write=$(this).find('writePlugIn').text();
								}else{
									write='－';
								}

								if($(this).find('recNamePlugIn').text().length>0){
									if($(this).find('recNamePlugIndll').text().length>0){
										recname=$(this).find('recNamePlugIndll').text();
										recnameval=$(this).find('recNamePlugIndll').text();
										op='<ul><li>オプション</li><li><input type="text" name="partialrecName" value="'+$(this).find('recNamePlugInoption').text()+'" size="25"></li></ul>'
									}else{
										recname=$(this).find('recNamePlugIn').text();
									}
								}else{
									recname='－';
								}
								$("#partialpreset").append('<input type=hidden name="partialrecFolder" value="' + folderval + '"><input type=hidden name="partialwritePlugIn" value="' + writeval + '"><input type=hidden name="partialrecNamePlugIn" value="' + recnameval + '">' +
								'<ul><li>フォルダ</li><li">' + folder + '</li></ul><ul><li>出力PlugIn</li><li>' + write + '</li></ul><ul><li>ファイル名PlugIn</li><li>' + recname + '</li></ul>').append(op);
							})
						}else{
							$("#partialpreset").append('<ul><li>フォルダ</li><li">－</li></ul><ul><li>出力PlugIn</li><li>－</li></ul><ul><li>ファイル名PlugIn</li><li>－</li></ul>');
						}
					}
				})
				if(!preset){dialog("Error: プリセットが見つかりません",true);}
			}
		})
	});

	$(".api").submit(function(event){			//予約等追加
		event.preventDefault();
		var form = $(this);
		$.ajax({
			url: form.attr('action'),
			type: form.attr('method'),
			data: form.serialize(),
			
			success: function(result, textStatus, xhr) {	//通信成功
				var xml=$(xhr.responseXML);
				if(xml.find('success').length>0){
					var messege=xml.find('success').text();
					if(typeof form.data("redirect") !== "undefined"){	//一覧(data-redirec)に戻る
						dialog(messege);
						setTimeout('location.href="'+form.data("redirect")+'";',1000);
					}else if(form.hasClass("submit")){			//検索(.submit)に戻る
						dialog(messege);
						setTimeout('$("#hidden").submit();',1000);
					}else{							//指定なし
						dialog(messege,true);
					}
				}else{
					dialog("Error: "+xml.find('err').text(),true);			//エラー表示
				}
			}
		});
	});
});
