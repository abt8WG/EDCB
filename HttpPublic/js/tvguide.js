function now(){								//現時刻の位置
	time()
	var t=0;if(HOUR<basehour){t=24;}
	return ((HOUR-basehour+t)*60+MIN)*oneminpx;
}

function line(){								//現時刻のライン表示
	$("#line").offset({top:now()+$("#tvguide").offset().top});
}

function minute(){
	line();
	if(min!=$("#line").text()){$("#line").text(min);}			//ラインに分を表示
}

function end(){
	minute();
	var end=$('.end_'+date+'-'+hour+'-'+min+'-'+sec);
	var hide='';
	if(end.hasClass('nothing')){hide=' style="display:none;"'}
	if(end.children().last().hasClass('content')){end.append('<div class="end"'+hide+'>');}
}

function jump(){
	$("#wrap_tv_3").animate({scrollTop:now()-marginmin*oneminpx}, 550, "swing");
}

function move(){								//ヘッダの位置
		$("#head").offset({left:$("#tvguide").offset().left});		//チャンネル名
		$("#hour").offset({top:$("#tvguide").offset().top});		//時間帯
}

function scroll(time){							//指定時間の位置
	var position=((time-1)*60-30)*oneminpx;
	$("#wrap_tv_3").animate({scrollTop:position}, 550, "swing");
}

$(function(){
	$(window).on("load resize", function(){					//ヘッダの微調整
		headerheight=$("#tvheader").height()+$("#head").height();
		$("#head").css("padding-top",$("#tvheader").height());
		$("#wrap_tv_1").css("padding-top",headerheight);
		$("#wrap_tv_2").css("padding-left",$("#hour").width())
		$("#tvheader style").remove();
		$("#tvheader").append('<style>#tvheader:after{width:'+($("#hour").width()-1)+'px;height:'+$("#head").height()+'px;bottom:-'+$("#head").height()+'px;}</style>');
		move();
	});

	$("#wrap_tv_3").on("scroll", function(){				//スクロール連動
		move();
		line();

		/*禁断の果実
		$.each($(".cell"), function(){
			base=$(this).offset().top-headerheight;
			height=$(this).innerHeight();
			content=$(this).children(".content");
			if(content.hasClass("reserved") || content.hasClass("disable") || content.hasClass("partially") || content.hasClass("shortage") || content.hasClass("viewing")){
				if(base<-3 && height+base>3){
					content.addClass("keep-top").css("top",-base).height(height+base-3).css("min-height",0);
					return
				}
			}else{
				if(base<0 && height+base>0){
					content.css("top",-base).height(height+base).css("min-height",0);
					return
				}
			}
			content.css("top",0).css("min-height",height).removeClass("keep-top");
		})
		$.each($("#hour div"), function(){
			base=$(this).offset().top-headerheight;
			height=$(this).innerHeight();
			content=$(this).find("span");
			if(base<0 && height+base>0){
				content.css("top",-base);
			}else{
				content.css("top",0);
			}
		})*/
	});

	$("[name=hide]").change(function(){
		var val=$(this).val();
		if($(this).prop('checked')){
			$(".id-"+val).hide();
		}else{
			$(".id-"+val).show();
		}
	});


	$("[id^=g-]").click(function(){
		if($(this).hasClass("active")){
			$("[id^=g-]").removeClass("active");
			$(".content").show();
			$(".end").show();
			$(".cell").removeClass("nothing");
		}else{
			$("[id^=g-]").removeClass("active");
			$(this).addClass("active");
			$(".content").hide();
			$(".end").hide();
			$(".cell").addClass("nothing");
			$(".content."+$(this).data("value")).show().parent(".cell").removeClass("nothing");
		}
	});
	

	$(".cell").mousedown(function(e){					//番組詳細表示
		if(e.which==1){
			$("#andKey").blur();
			pageX=e.pageX
			pageY=e.pageY
		}
	}).mouseup(function(e){
		if(pageX==e.pageX && pageY==e.pageY && e.which==1){				//ドラッグスクロール排除
			if(!$(e.target).is("a,label,.nothing")){
				if($(this).hasClass("clicked")){
					$(this).removeClass("clicked");
				}else{
					$(".cell").removeClass("clicked");
					$(this).addClass("clicked");
				}
			}
		}else if (hover==false && e.which==1){
			$(".cell").removeClass("clicked");
		}
	});

	if (hover==true){
		$(".cell").hover(						//マウスホバーで番組詳細表示
			function(){
				$(this).addClass("clicked");
			},function(){
				$(this).removeClass("clicked");
		});
	}

	$("#jumpnow").click(function(){jump();});				//現時刻に移動

	$("a.jumper").click(function(){						//指定時間に移動
		scroll($(this).attr("value"));
	});
	$("[name=time]").change(function(){					//指定時間に移動(スマホ用)
		if ($(this).val().match(/^epg/i)){
			location.href=$(this).val();
		}else{
			scroll($(this).val());
		}
	});

	$(".pull").hover(							//サブメニュー表示
		function(){
			$(this).addClass("hover");
			var width=$(".hover ul").innerWidth();
			var height=$(".hover ul").innerHeight();
			$(".hover div").width(width).height(height);
			$(".hover .jump").css("right",0);
			$(".hover ul").css("clip","rect(0px "+width+"px "+height+"px 0px)");
		}
		,function(){
			var width=$(".hover ul").innerWidth();
			$(".hover ul").css("clip","rect(0px "+width+"px 0px "+width+"px)");
			$(this).removeClass("hover");
		}
	);

	$(".toggle").click(function(){
		$(this).toggleClass("clicked").next().slideToggle()
	});

	$('.addepg').click(function(){						//EPG予約
		$('#hidden [name="andKey"]').val($(this).data('andkey'));
		var service=$('#hidden [name="serviceList"]');
		if(service.val()==''){service.val($(this).parents('[class^="id-"]').data('service'))};
		$('#hidden').submit();
	});

	$(".reserve").click(function(){
		var a=this;
		var url ,data;
		if ($(this).data("reserve")>0){
			url="api/reservetoggle";
			data={"id":$(this).data("reserve")};
		}else if ($(this).data("eid")>0){
			url="api/oneclickadd";
			data={	"onid":$(this).data("onid"),
				"tsid":$(this).data("tsid"),
				"sid":$(this).data("sid"),
				"eid":$(this).data("eid")};
		}
		$.ajax({
			url: url,
			data: data,

			success: function(result, textStatus, xhr) {
				var xml=xhr.responseXML;
				if($(xml).find('success').length>0){
					var start=$(xml).find('start').text();
					var recmode=$(xml).find('recmode').text();
					var overlapmode=$(xml).find('overlapmode').text();
					var id=$(xml).find('reserveid').text();

					var parents=$(a).parents(".cell")
					$(a).data("reserve", id);
					$(parents).children().removeClass('disable partially shortage viewing reserved');
					if($(parents).find('.recable').length==0){
						$(parents).find('.start').append('<span class="mark recable"></span>');
					}
					if (recmode==5){
						$(a).html('有効');
						$(parents).children().not('.end').addClass('disable').find('.recable').addClass('no').html('無');
					}else{
						$(a).html('無効');
						$(parents).find('.recable').removeClass('no view');
						if(overlapmode==1){
							$(parents).children().not('.end').addClass('partially').find('.recable').html('部');
						}else if(overlapmode==2){
							$(parents).children().not('.end').addClass('shortage').find('.recable').html('不');
						}else if(recmode==4){
							$(parents).children().not('.end').addClass('viewing').find('.recable').addClass('view').html('視');
						}else{
							$(parents).children().not('.end').addClass('reserved').find('.recable').html('録');
						}
					}
				}else{
					dialog("Error: "+$(xml).find('err').text(),true);
				}
				$(".cell").removeClass("clicked");
			}
		})
	});

});
