function set_reset(){
	$('#set')[0].reset();
	$('.background').css('background',$('[name=background]').val());
	$('.news').css('background',$('[name=news]').val());
	$('.sports').css('background',$('[name=sports]').val());
	$('.information').css('background',$('[name=information]').val());
	$('.drama').css('background',$('[name=drama]').val());
	$('.music').css('background',$('[name=music]').val());
	$('.variety').css('background',$('[name=variety]').val());
	$('.movie').css('background',$('[name=movie]').val());
	$('.anime').css('background',$('[name=anime]').val());
	$('.documentary').css('background',$('[name=documentary]').val());
	$('.theater').css('background',$('[name=theater]').val());

	$('.education').css('background',$('[name=education]').val());
	$('.welfare').css('background',$('[name=welfare]').val());
	$('.extension').css('background',$('[name=extension]').val());
	$('.other').css('background',$('[name=other]').val());
	$('.none').css('background',$('[name=none]').val());
	$('.nothing').css('background',$('[name=nothing]').val());

	if (paint==true){
		$('.reserved').css('border','none').css('background',$('[name=reserved]').val());
		$('.disable').css('border','none').css('background',$('[name=disable]').val());
	}else{
		$('.reserved').css('border','3px dotted '+$('[name=reserved]').val()).css('background','transparent');
		$('.disable').css('border','3px dotted '+$('[name=disable]').val()).css('background','transparent');
	}
	$('.partially').css('background',$('[name=partially]').val());
	$('.partially').css('border-color',$('[name=partially_border]').val());
	$('.shortage').css('background',$('[name=shortage]').val());
	$('.shortage').css('border-color',$('[name=shortage_border]').val());
	$.each(
		dtv.split(","),
		function(index, value) {
			$('#'+value).appendTo("#service .dtv");
		}
	);
	$.each(
		oneseg.split(","),
		function(index, value) {
			$('#'+value).appendTo("#service .oneseg");
		}
	);
	$.each(
		bs.split(","),
		function(index, value) {
			$('#'+value).appendTo("#service .bs");
		}
	);
	$.each(
		cs.split(","),
		function(index, value) {
			$('#'+value).appendTo("#service .cs");
		}
	);
	$.each(
		other.split(","),
		function(index, value) {
			$('#'+value).appendTo("#service .other");
		}
	);
}

function reset_settings(val,section){
	dialog(val+'を初期化しますか？','close del','閉じる');
	$('#dialog .footer').append('<input class="button reset_settings" data-section="'+section+'" type="button" value="初期化">');
}



$(function(){
	$(document).on("click",".reset_settings",function(){
		$("#dialog .card").removeClass("visible");
		$.ajax({
			url: 'api/SaveSettings',
			type: 'GET',
			data: {'reset':$(this).data("section")},
			success: function(result, textStatus, xhr) {
				var xml=$(xhr.responseXML);
				if(xml.find('success').length>0){
					dialog(xml.find('success').text(),"reload","更新");
				}else{
					dialog("Error: "+xml.find('err').text(),true);
				}
			}
		})
	});

	$("#tab label").click(function(){			//タブ切替
		$("#tab label").removeClass("active");
		$(this).addClass("active");
		$("#service ul").hide();
		$("."+$(this).data("val")).show();
	});
	$("#service ul").disableSelection();					//並び替え
	$("#service ul").sortable({handle: ".handle"});
	if($("#service .dtv").length>0){dtv = $("#service .dtv").sortable("toArray").join(",");	}else{dtv=""}		//初期値保存
	if($("#service .oneseg").length>0){oneseg = $("#service .oneseg").sortable("toArray").join(",");}else{oneseg=""}
	if($("#service .bs").length>0){bs = $("#service .bs").sortable("toArray").join(",");}else{bs=""}
	if($("#service .cs").length>0){cs = $("#service .cs").sortable("toArray").join(",");}else{cs=""}
	if($("#service .other").length>0){other = $("#service .other").sortable("toArray").join(",");}else{other=""}

	$('.genre [type=color],[name=background]').change(function(){
		$('.'+$(this).attr("name")).css('background',$(this).val());
	});
	
	$('[name=reserved],[name=disable]').change(function(){
		if ($('#paint').prop('checked')){
			$('.'+$(this).attr("name")).css('background',$(this).val());
		}else{
			$('.'+$(this).attr("name")).css('border-color',$(this).val());
		}
	});
	$('[name=partially],[name=shortage]').change(function(){
			$('.'+$(this).attr("name")).css('background',$(this).val());
	});
	$('[name=partially_border]').change(function(){
			$('.partially').css('border-color',$(this).val());
	});
	$('[name=shortage_border]').change(function(){
			$('.shortage').css('border-color',$(this).val());
	});
	$('#paint').change(function(){
		if ($(this).prop('checked')){
			$('.reserved').css('border','none').css('background',$('[name=reserved]').val());
			$('.disable').css('border','none').css('background',$('[name=disable]').val());
		}else{
			$('.reserved').css('border','3px dotted '+$('[name=reserved]').val()).css('background','transparent');
			$('.disable').css('border','3px dotted '+$('[name=disable]').val()).css('background','transparent');
		}
	});

});
