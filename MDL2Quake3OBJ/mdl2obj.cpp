
#include "mdlviewer_stuff/StudioModel.h"
#include "mdlviewer_stuff/ControlPanel.h"
#include <stdio.h>
#include <iostream>
#include <fstream>
#include "mdlviewer_stuff/ViewerSettings.h"

// These are just so I don't get errors compiling, we're not rly drawing anything in this anyway
vec3_t g_vright;		// needs to be set to viewer's right in order for chrome to work
float g_lambert;		// modifier for pseudo-hemispherical lighting


int main(int argc, char** argv) {

	if (argc < 3) return 1;

	const char* filename = argv[1];
	const char* filenameOut = argv[2];

	char* fileNameOutMtl = new char[strlen(filenameOut)+1];
	strcpy(fileNameOutMtl, filenameOut);
	char* dotChar = strrchr(fileNameOutMtl, '.');
	if (!dotChar) {
		std::cout << "output file doesn't have file ending?";
		return 1;
	}
	if (dotChar == fileNameOutMtl) {
		std::cout << "output file only has a file ending?";
		return 1;
	}
	if (strlen(dotChar) != 4) {
		std::cout << "output file ending does not have 3 characters?";
		return 1;
	}
	*(dotChar+1) = 'm';
	*(dotChar+2) = 't';
	*(dotChar+3) = 'l';


	std::cout << "mdl 2 obj processing: \n";
	std::cout << filename << "\n";
	std::cout << filenameOut << "\n";
	std::cout << fileNameOutMtl << "\n";

	ControlPanel panel;
	panel.loadModel(filename);

	std::stringstream ss;
	std::stringstream ssMtl;

	ss << "mtllib "<< fileNameOutMtl <<"\n";

	g_studioModel.WriteModel(&ss, &ssMtl);

	std::ofstream objFile(filenameOut);
	objFile << ss.str();
	objFile.close();

	std::ofstream mtlFile(fileNameOutMtl);
	mtlFile << ssMtl.str();
	mtlFile.close();

	std::cout << "conversion done.";

	delete[] fileNameOutMtl;
	return 0;
}