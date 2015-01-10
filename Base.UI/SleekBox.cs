using System;
using UnityEngine;

public class SleekBox : SleekLabel
{
	public SleekBox()
	{
	}

	public override void drawFrame()
	{
		base.update();
		GUI.skin.label.fontSize = this.fontSize;
		GUI.skin.label.alignment = this.alignment;
		SleekRender.box(new Rect((float)base.getPosition_x(), (float)base.getPosition_y(), (float)base.getSize_x(), (float)base.getSize_y()), this.text, this.tooltip, this.color, this.paint);
		base.drawChildren();
	}
}