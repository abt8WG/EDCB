#include "StdAfx.h"
#include "NITTable.h"

void CNITTable::Clear()
{
	descriptorList.clear();
	TSInfoList.clear();
}

BOOL CNITTable::Decode( BYTE* data, DWORD dataSize, DWORD* decodeReadSize )
{
	if( InitDecode(data, dataSize, decodeReadSize, TRUE) == FALSE ){
		return FALSE;
	}
	Clear();

	if( section_syntax_indicator != 1 ){
		//固定値がおかしい
		_OutputDebugString( L"++CNITTable:: section_syntax err" );
		return FALSE;
	}
	if( table_id != 0x40 && table_id != 0x41 ){
		//table_idがおかしい
		_OutputDebugString( L"++CNITTable:: table_id err 0x%02X", table_id );
		return FALSE;
	}

	if( section_length - 4 > 8 ){
		network_id = ((WORD)data[readSize])<<8 | data[readSize+1];
		version_number = (data[readSize+2]&0x3E)>>1;
		current_next_indicator = data[readSize+2]&0x01;
		section_number = data[readSize+3];
		last_section_number = data[readSize+4];
		network_descriptors_length = ((WORD)data[readSize+5]&0x0F)<<8 | data[readSize+6];
		readSize += 7;
		if( readSize+network_descriptors_length <= (DWORD)section_length+3-4 && network_descriptors_length > 0){
			if( network_id == 0x0001 || network_id == 0x0003 ){
				SDDecode( data+readSize, network_descriptors_length, &descriptorList, NULL );
			}else{
				if( AribDescriptor::CreateDescriptors( data+readSize, network_descriptors_length, &descriptorList, NULL ) == FALSE ){
					_OutputDebugString( L"++CNITTable:: descriptor err" );
					return FALSE;
				}
			}
			readSize+=network_descriptors_length;
		}
		transport_stream_loop_length = ((WORD)data[readSize]&0x0F)<<8 | data[readSize+1];
		readSize += 2;
		WORD tsLoopReadSize = 0;
		while( readSize+5 < (DWORD)section_length+3-4 && tsLoopReadSize < transport_stream_loop_length){
			TSInfoList.push_back(TS_INFO_DATA());
			TS_INFO_DATA* item = &TSInfoList.back();
			item->transport_stream_id = ((WORD)data[readSize])<<8 | data[readSize+1];
			item->original_network_id = ((WORD)data[readSize+2])<<8 | data[readSize+3];
			item->transport_descriptors_length = ((WORD)data[readSize+4]&0x0F)<<8 | data[readSize+5];
			readSize += 6;
			if( readSize+item->transport_descriptors_length <= (DWORD)section_length+3-4 && item->transport_descriptors_length > 0){
				if( AribDescriptor::CreateDescriptors( data+readSize, item->transport_descriptors_length, &(item->descriptorList), NULL ) == FALSE ){
					_OutputDebugString( L"++CNITTable:: descriptor2 err" );
					return FALSE;
				}
			}

			readSize+=item->transport_descriptors_length;
			tsLoopReadSize += 6 + item->transport_descriptors_length;
		}
	}else{
		return FALSE;
	}

	return TRUE;
}

BOOL CNITTable::SDDecode( BYTE* data, DWORD dataSize, vector<AribDescriptor::CDescriptor>* descriptorList_, DWORD* decodeReadSize )
{
	BOOL ret = TRUE;
	if( data == NULL || dataSize == 0 || descriptorList_ == NULL ){
		return FALSE;
	}
	DWORD decodeSize = 0;

	descriptorList_->push_back(AribDescriptor::CDescriptor());
	AribDescriptor::CDescriptor* item = &descriptorList_->back();

	static const short parser0x82[] = {
		AribDescriptor::descriptor_tag, 8,
		AribDescriptor::descriptor_length, AribDescriptor::D_LOCAL, 8,
		AribDescriptor::D_BEGIN, AribDescriptor::descriptor_length,
			AribDescriptor::reserved, AribDescriptor::D_LOCAL, 8,
			AribDescriptor::d_char, AribDescriptor::D_STRING_TO_END,
		AribDescriptor::D_END,
		AribDescriptor::D_FIN,
	};
	AribDescriptor::PARSER_PAIR parserList[] = {{0x82, parser0x82}, {0, NULL}};

	while( decodeSize + 2 < dataSize ){
		BYTE* readPos = data+decodeSize;
		if( readPos[0] == 0x82 ){
			//サービス名
			if( readPos[2] == 0x01 ){
				//日本語版？
				if( item->Decode(readPos, dataSize - decodeSize, NULL, parserList) != false ){
					//ネットワーク名記述子にキャスト
					item->SetNumber(AribDescriptor::descriptor_tag, AribDescriptor::network_name_descriptor);
				}
			}
			decodeSize += readPos[1]+2;
		}else{
			decodeSize += readPos[1]+2;
		}
	}
	if( item->Has(AribDescriptor::d_char) == false ){
		descriptorList_->pop_back();
		ret = FALSE;
	}

	if( decodeReadSize != NULL ){
		*decodeReadSize = dataSize;
	}
	return ret;
}
