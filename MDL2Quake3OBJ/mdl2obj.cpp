
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

	if (argc < 2) return 1;

	const char* filename = argv[1];

	ControlPanel panel;
	panel.loadModel(filename);

	std::stringstream ss;

	g_studioModel.WriteModel(&ss);

	std::ofstream objFile("test.obj");
	objFile << ss.str();
	objFile.close();

	std::cout << "hello world";
	return 0;
}