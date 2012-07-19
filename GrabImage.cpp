// GrabImage.cpp : Defines the entry point for the DLL application.
//

#include <streams.h>
#include <qedit.h>

#include <windows.h>
#include <atlbase.h>

#include "stdafx.h"
#include "GrabImage.h"

BOOL APIENTRY DllMain( HANDLE hModule, 
                       DWORD  ul_reason_for_call, 
                       LPVOID lpReserved
					 )
{
	switch (ul_reason_for_call)
	{
	case DLL_PROCESS_ATTACH:
	case DLL_THREAD_ATTACH:
	case DLL_THREAD_DETACH:
	case DLL_PROCESS_DETACH:
		break;
	}
    return TRUE;
}

HRESULT ConnectFilters(
    IGraphBuilder *pGraph, 
    IBaseFilter *pSrc, 
    IBaseFilter *pDest);

HRESULT SaveGraphFile(IGraphBuilder *pGraph, WCHAR *wszPath);

HRESULT hr = NULL;

IGraphBuilder *pGraph = NULL;
ICaptureGraphBuilder2 *pBuilder = NULL;
IBaseFilter *pVideoCap = NULL;
IBaseFilter *pGrabberF = NULL;
ISampleGrabber *pGrabber = NULL;
IVideoWindow *pVideoWindow = NULL;
IBaseFilter *pNull = NULL;

#ifdef _DEBUG
FILE *stream = NULL;
#endif

AM_MEDIA_TYPE mt;

HWND hWnd;

double ASPECT_RATIO = 1.2;

//************************************************************************************
void GRABIMAGE_API __stdcall StartPreview(HWND hW) {
	hWnd = hW;
//	LPCWSTR g_PathFileName = L"C:\\Documents and Settings\\roman\\My Documents\\Roxio\\VideoWave 5 Power\\Capture\\NONAME_004.AVI";

	DestroyGraph();

#ifdef _DEBUG
	stream = fopen("grab_image.txt", "w");
#endif

	// Initialize the COM library.
    hr = CoInitializeEx(NULL, COINIT_APARTMENTTHREADED);
    if (FAILED(hr)) {
//        printf("ERROR - Could not initialize COM library");
#ifdef _DEBUG
		fclose( stream );
#endif
        return;
    }
    
	// Create the Filter Graph Manager object and retrieve its
    // IGraphBuilder interface.
    hr = CoCreateInstance(CLSID_FilterGraph, NULL, CLSCTX_INPROC_SERVER,
                           IID_IGraphBuilder, (void **)&pGraph);
    if (FAILED(hr)) { 
//        printf("ERROR - Could not create the Filter Graph Manager.");
        CoUninitialize();        
#ifdef _DEBUG
		fclose( stream );
#endif
//        return hr;
        return;
    }

    // Create the Capture Graph Builder.
    hr = CoCreateInstance(CLSID_CaptureGraphBuilder2, NULL,
							CLSCTX_INPROC_SERVER, IID_ICaptureGraphBuilder2, 
							(void **)&pBuilder);

    if (SUCCEEDED(hr)) {
        pBuilder->SetFiltergraph(pGraph);
    }

	ICreateDevEnum *pDevEnum = NULL;
	IEnumMoniker *pEnum = NULL;

	// Create the System Device Enumerator.
	hr = CoCreateInstance(CLSID_SystemDeviceEnum, NULL,
				CLSCTX_INPROC_SERVER, IID_ICreateDevEnum, 
				reinterpret_cast<void**>(&pDevEnum));

	if (SUCCEEDED(hr)) {
		// Create an enumerator for the video capture category.
		hr = pDevEnum->CreateClassEnumerator(
								CLSID_VideoInputDeviceCategory,
								&pEnum, 0);


		USES_CONVERSION;
		WCHAR wachFriendlyName[120];

		IMoniker *pMoniker = NULL;
		while (pEnum->Next(1, &pMoniker, NULL) == S_OK) {
			IPropertyBag *pPropBag;
			wachFriendlyName[0] = 0;
			hr = pMoniker->BindToStorage(0, 0, IID_IPropertyBag, 
												(void**)(&pPropBag));
			if (FAILED(hr)) {
				pMoniker->Release();
				continue;  // Skip this one, maybe the next one will work.
			}

			// Find the description or friendly name.
			VARIANT varName;
//            varName.vt = VT_BSTR;
			VariantInit(&varName);
			hr = pPropBag->Read(L"Description", &varName, 0);
			if (FAILED(hr)) {
				hr = pPropBag->Read(L"FriendlyName", &varName, 0);
				if(hr == NOERROR) {
					lstrcpyW(wachFriendlyName, varName.bstrVal);
					SysFreeString(varName.bstrVal);
				}
			}

			VariantClear(&varName); 

			if (SUCCEEDED(hr)) {
				hr = pMoniker->BindToObject(0, 0, IID_IBaseFilter, (void**)&pVideoCap);
				if (SUCCEEDED(hr)) {
					// Add the video capture filter to the graph with its friendly name
					hr = pGraph->AddFilter(pVideoCap, wachFriendlyName);
				}
			}

			IAMAnalogVideoDecoder *pVDecoder;
			hr = pBuilder->FindInterface(&PIN_CATEGORY_CAPTURE,
                                          &MEDIATYPE_Video,
										  pVideoCap,
                                          IID_IAMAnalogVideoDecoder, (void **)&pVDecoder);
			if(SUCCEEDED(hr)) {
//					int AnalogVideo_NTSC_M     = 0x00000001, 
//					int AnalogVideo_PAL_B      = 0x00000010,

				pVDecoder->put_TVFormat(AnalogVideo_PAL_B);
                pVDecoder->Release();
			}

			pPropBag->Release();
			pMoniker->Release();
		}
	}

	pDevEnum->Release();
	pEnum->Release();

	// Create the Sample Grabber.
	hr = CoCreateInstance(CLSID_SampleGrabber, NULL, CLSCTX_INPROC_SERVER,
							IID_IBaseFilter, (void**)&pGrabberF);

    if (FAILED(hr)) { 
//        printf("ERROR - Could not create the Sample Grabber Filter.");
        pVideoCap->Release();
        pBuilder->Release();
        pGraph->Release();
		pGraph = NULL;
        CoUninitialize();        
#ifdef _DEBUG
		fclose( stream );
#endif
//        return hr;
        return;
    }

	hr = pGraph->AddFilter(pGrabberF, L"Sample Grabber");
	if (FAILED(hr)) {
//        printf("ERROR - Could not add the Sample Grabber Filter.");
        pGrabberF->Release();
        pVideoCap->Release();
        pBuilder->Release();
        pGraph->Release();
		pGraph = NULL;
        CoUninitialize();        
#ifdef _DEBUG
		fclose( stream );
#endif
//        return hr;
        return;
	}

    // Query the Sample Grabber for the ISampleGrabber interface.
	pGrabberF->QueryInterface(IID_ISampleGrabber, (void**)&pGrabber);
    if (FAILED(hr)) {
//        printf("ERROR - Could not get the ISampleGrabber interface.");
        pGrabberF->Release();
        pVideoCap->Release();
        pBuilder->Release();
        pGraph->Release();
		pGraph = NULL;
        CoUninitialize();
#ifdef _DEBUG
		fclose( stream );
#endif
//        return hr;
        return;
    }

	ZeroMemory(&mt, sizeof(AM_MEDIA_TYPE));
	mt.majortype = MEDIATYPE_Video;
	mt.subtype = MEDIASUBTYPE_RGB24;

	// Set the Media Type
	pGrabber->SetMediaType(&mt);
	pGrabber->SetBufferSamples(TRUE);


	hr = pBuilder->RenderStream(&PIN_CATEGORY_CAPTURE, &MEDIATYPE_Video, pVideoCap, NULL, pGrabberF);
	hr = pBuilder->RenderStream(&PIN_CATEGORY_PREVIEW, &MEDIATYPE_Video, pVideoCap, NULL, NULL);

	hr = pGraph->QueryInterface(IID_IVideoWindow, (void **)&pVideoWindow);
	if(hr == NOERROR) {
		pVideoWindow->put_Owner((OAHWND)hWnd);
		pVideoWindow->put_WindowStyle(WS_CHILD | WS_CLIPSIBLINGS);

		IBasicVideo *pBasicVideo;
		hr = pGraph->QueryInterface(IID_IBasicVideo, (void **)&pBasicVideo);
		if(hr == NOERROR) {
			LONG lWidth = 0;
			LONG lHeight = 0;
			hr = pBasicVideo->GetVideoSize(&lWidth, &lHeight);
			pBasicVideo->put_SourceWidth(lHeight / ASPECT_RATIO);
			pBasicVideo->Release();

		}

		RECT rc;
		GetClientRect(hWnd, &rc);

		pVideoWindow->SetWindowPosition(0, 0, rc.right, rc.bottom);
		pVideoWindow->put_Visible(OATRUE);
	}

	// Create the Null Renderer Filter.
	hr = CoCreateInstance(CLSID_NullRenderer, NULL, CLSCTX_INPROC_SERVER,
							IID_IBaseFilter, reinterpret_cast<void**>(&pNull));
	if (FAILED(hr)) {
//        printf("ERROR - Could not create the Null Renderer Filter.");
		FreeMediaType(mt);
        pVideoWindow->Release();
        pGrabber->Release();
        pGrabberF->Release();
        pVideoCap->Release();
        pBuilder->Release();
        pGraph->Release();
		pGraph = NULL;
        CoUninitialize();
#ifdef _DEBUG
		fclose( stream );
#endif
//        return hr;
        return;
    }
	
	hr = pGraph->AddFilter(pNull, L"NullRenderer");
	if (FAILED(hr)) {
//        printf("ERROR - Could not add the Null Renderer Filter.");
		FreeMediaType(mt);
        pNull->Release();
        pVideoWindow->Release();
        pGrabber->Release();
        pGrabberF->Release();
        pVideoCap->Release();
        pBuilder->Release();
        pGraph->Release();
		pGraph = NULL;
        CoUninitialize();
#ifdef _DEBUG
		fclose( stream );
#endif
        return;
    }

	hr = ConnectFilters(pGraph, pGrabberF, pNull);
//#ifdef _DEBUG
//	fprintf(stream, "Return code: is %d\n", hr);
//#endif

	// Run the graph.
	IMediaControl *pMC = NULL;
    hr = pGraph->QueryInterface(IID_IMediaControl, (void **)&pMC);
    if(SUCCEEDED(hr)) {
        hr = pMC->Run();
        pMC->Release();
    }

#ifdef _DEBUG
	// Before we finish, save the filter graph to a file.
	SaveGraphFile(pGraph, L"C:\\MyGraph.GRF");
#endif

	return;
}

//************************************************************************************
long GRABIMAGE_API __stdcall MakeOneShot(void) {
	if (pGraph == NULL)
		return 0;

	// Set one-shot mode and buffering.
	pGrabber->SetOneShot(TRUE);

    // stop the graph
	IMediaControl *pMC = NULL;
    hr = pGraph->QueryInterface(IID_IMediaControl, (void **)&pMC);
    if(SUCCEEDED(hr))
    {
        hr = pMC->Stop();
        pMC->Release();
    }

	// Grab the Sample
	// Find the required buffer size.
	long cbBuffer = 0;
	hr = pGrabber->GetCurrentBuffer(&cbBuffer, NULL);
//	fprintf(stream, "Buffer length is %d\n", cbBuffer);
	if (cbBuffer <= 0) {
//        printf("ERROR - Could not get connected media type.");
		FreeMediaType(mt);
        pNull->Release();
        pVideoWindow->Release();
        pGrabber->Release();
        pGrabberF->Release();
        pVideoCap->Release();
        pBuilder->Release();
        pGraph->Release();
		pGraph = NULL;
        CoUninitialize();
#ifdef _DEBUG
		fclose( stream );
#endif
		return 0;
	}

	hr = pGrabber->GetConnectedMediaType(&mt);
	if (FAILED(hr)) {
//        printf("ERROR - Could not get connected media type.");
		FreeMediaType(mt);
        pNull->Release();
        pVideoWindow->Release();
        pGrabber->Release();
        pGrabberF->Release();
        pVideoCap->Release();
        pBuilder->Release();
        pGraph->Release();
		pGraph = NULL;
        CoUninitialize();
#ifdef _DEBUG
		fclose( stream );
#endif
		return 0;
	}

	// Examine the format block.
	VIDEOINFOHEADER *pVih;
	if ((mt.formattype == FORMAT_VideoInfo) && 
		(mt.cbFormat >= sizeof(VIDEOINFOHEADER)) &&
		(mt.pbFormat != NULL) ) {
		pVih = (VIDEOINFOHEADER*)mt.pbFormat;
	} else {
		// Wrong format. Free the format block and return an error.
//        printf("ERROR - Wrong format.");
		FreeMediaType(mt);
        pNull->Release();
        pVideoWindow->Release();
        pGrabber->Release();
        pGrabberF->Release();
        pVideoCap->Release();
        pBuilder->Release();
        pGraph->Release();
		pGraph = NULL;
        CoUninitialize();
#ifdef _DEBUG
		fclose( stream );
#endif
		return 0;
	}

	pVih->bmiHeader.biWidth = pVih->bmiHeader.biHeight / ASPECT_RATIO;
	int newStride = ((pVih->bmiHeader.biWidth + 3) & ~3) * pVih->bmiHeader.biBitCount / 8;
	pVih->bmiHeader.biSizeImage = newStride * pVih->bmiHeader.biHeight;
	
	DWORD bfSize = (DWORD) (sizeof(BITMAPFILEHEADER) + 
							pVih->bmiHeader.biSize + 
							pVih->bmiHeader.biClrUsed * sizeof(RGBQUAD) +
							pVih->bmiHeader.biSizeImage); 
/*
	DWORD bfSize = (DWORD) (sizeof(BITMAPFILEHEADER) + 
							pVih->bmiHeader.biSize + 
							pVih->bmiHeader.biClrUsed * sizeof(RGBQUAD) +
							cbBuffer); 
*/
	return bfSize;
}

//************************************************************************************
//void GRABIMAGE_API __stdcall GetBitmap(LPBYTE lpBitsPB) {
void GRABIMAGE_API __stdcall TakeSnap(char* fileName) {

	long cbBuffer = 0;
	hr = pGrabber->GetCurrentBuffer(&cbBuffer, NULL);
//	fprintf(stream, "Buffer length is %d\n", cbBuffer);

	char *pBuffer = new char[cbBuffer];
	char *pBuffOrg = pBuffer;

	if (!pBuffer) {
		// Out of memory.
//        printf("ERROR - Out of memory.");
		FreeMediaType(mt);
        pNull->Release();
        pVideoWindow->Release();
        pGrabber->Release();
        pGrabberF->Release();
        pVideoCap->Release();
        pBuilder->Release();
        pGraph->Release();
		pGraph = NULL;
        CoUninitialize();
#ifdef _DEBUG
		fclose( stream );
#endif
		return;
	}

	hr = pGrabber->GetCurrentBuffer(&cbBuffer, (long*)pBuffer);

	hr = pGrabber->GetConnectedMediaType(&mt);
	if (FAILED(hr)) {
//        printf("ERROR - Could not get connected media type.");
		FreeMediaType(mt);
        pNull->Release();
        pVideoWindow->Release();
        pGrabber->Release();
        pGrabberF->Release();
        pVideoCap->Release();
        pBuilder->Release();
        pGraph->Release();
		pGraph = NULL;
        CoUninitialize();
#ifdef _DEBUG
		fclose( stream );
#endif
		return;
	}

	// Examine the format block.
	VIDEOINFOHEADER *pVih;
	if ((mt.formattype == FORMAT_VideoInfo) && 
		(mt.cbFormat >= sizeof(VIDEOINFOHEADER)) &&
		(mt.pbFormat != NULL) ) {
		pVih = (VIDEOINFOHEADER*)mt.pbFormat;
	} else {
		// Wrong format. Free the format block and return an error.
//        printf("ERROR - Wrong format.");
		FreeMediaType(mt);
        pNull->Release();
        pVideoWindow->Release();
        pGrabber->Release();
        pGrabberF->Release();
        pVideoCap->Release();
        pBuilder->Release();
        pGraph->Release();
		pGraph = NULL;
        CoUninitialize();
#ifdef _DEBUG
		fclose( stream );
#endif
		return;
	}

    BITMAPFILEHEADER hdr;       // bitmap file-header 
    hdr.bfType = 0x4d42;        // 0x42 = "B" 0x4d = "M" 

	// Compute the size of the entire file. 

	// (Round the image width up to a DWORD boundary.)
	int origStride = ((pVih->bmiHeader.biWidth + 3) & ~3) * pVih->bmiHeader.biBitCount / 8;
	pVih->bmiHeader.biWidth = pVih->bmiHeader.biHeight / ASPECT_RATIO;
	int newStride = ((pVih->bmiHeader.biWidth + 3) & ~3) * pVih->bmiHeader.biBitCount / 8;
	pVih->bmiHeader.biSizeImage = newStride * pVih->bmiHeader.biHeight;
	
	hdr.bfSize = (DWORD) (sizeof(BITMAPFILEHEADER) + 
							pVih->bmiHeader.biSize + 
							pVih->bmiHeader.biClrUsed * sizeof(RGBQUAD) +
							pVih->bmiHeader.biSizeImage); 
/*
	hdr.bfSize = (DWORD) (sizeof(BITMAPFILEHEADER) + 
							pVih->bmiHeader.biSize + 
							pVih->bmiHeader.biClrUsed * sizeof(RGBQUAD) +
							cbBuffer); 
*/
	hdr.bfReserved1 = 0; 
    hdr.bfReserved2 = 0; 

    // Compute the offset to the array of color indices. 
    hdr.bfOffBits = (DWORD) sizeof(BITMAPFILEHEADER) + 
							pVih->bmiHeader.biSize + 
							pVih->bmiHeader.biClrUsed * sizeof(RGBQUAD); 

	LPBYTE lpBits = (LPBYTE) GlobalAlloc(GMEM_FIXED, hdr.bfSize);

	LPBYTE lpBuff = lpBits;
	memcpy (lpBuff, &hdr, sizeof(BITMAPFILEHEADER));
	lpBuff += sizeof(BITMAPFILEHEADER);
	memcpy (lpBuff, &pVih->bmiHeader, sizeof(BITMAPINFOHEADER));
	lpBuff += sizeof(BITMAPINFOHEADER);
//	memcpy (lpBuff, pBuffer, cbBuffer);

	for (int i = 0; i < pVih->bmiHeader.biHeight; i++) {
		memcpy (lpBuff, pBuffer, newStride);
		lpBuff += newStride;
		pBuffer += origStride;
	}

//	memcpy (lpBitsPB, lpBits, hdr.bfSize);

//#ifdef _DEBUG
	HANDLE hf;					// file handle 
    DWORD dwTmp; 
	// Create the .BMP file. 
	hf = CreateFile(fileName, 
					GENERIC_READ | GENERIC_WRITE, 
					(DWORD) 0, 
                    NULL, 
					CREATE_ALWAYS, 
					FILE_ATTRIBUTE_NORMAL, 
					(HANDLE) NULL);

    // Copy the array of color indices into the .BMP file. 
    WriteFile(hf, (LPSTR) lpBits, (int) hdr.bfSize, (LPDWORD) &dwTmp, NULL);

	// Close the .BMP file. 
    CloseHandle(hf); 
//#endif

	GlobalFree((HGLOBAL)lpBits);
	delete[] pBuffOrg;

	DestroyGraph();
}


//************************************************************************************
void GRABIMAGE_API __stdcall DestroyGraph(void) {
	if (pGraph != NULL) {
//		hr = pGraph->Abort();
/*
		// Set one-shot mode and buffering.
		pGrabber->SetOneShot(TRUE);
*/
		// stop the graph
		IMediaControl *pMC = NULL;
		hr = pGraph->QueryInterface(IID_IMediaControl, (void **)&pMC);
		if(SUCCEEDED(hr)) {
			hr = pMC->Stop();
			pMC->Release();
		}

		FreeMediaType(mt);
		pNull->Release();
		pVideoWindow->Release();
		pGrabber->Release();
		pGrabberF->Release();
		pVideoCap->Release();
		pBuilder->Release();
		pGraph->Release();
		pGraph = NULL;
		CoUninitialize();
#ifdef _DEBUG
		fclose( stream );
#endif
	}
}

/*
// This is the constructor of a class that has been exported.
// see GrabImage.h for the class definition
CGrabImage::CGrabImage()
{ 
	return; 
}
*/
