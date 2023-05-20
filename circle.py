import matplotlib.pyplot as plt
import pandas as pd
from math import sqrt
from scipy.ndimage import gaussian_filter
from io import StringIO
from skimage.restoration import denoise_tv_chambolle
import numpy as np

def preprocess(file, smooth = 15):
    with open("./" + file, 'r') as file:
        file_content = file.read()
    file_content = file_content.replace(',', '.')

    with StringIO(file_content) as file_buffer:
        data = np.genfromtxt(file_buffer, delimiter='\t')

    time = data[:, 0]
    voltage = denoise_tv_chambolle((data[:, 1]), smooth)
    voltage = gaussian_filter(voltage, 1)
    return time, voltage


def circle(time, voltage):
    def nearest_value(items_x, items_y, value_x, value_y):
        return np.argmin(np.sqrt((items_x - value_x) ** 2 + (items_y - value_y) ** 2))

    def flat(x, y, n):
        return x[::n], y[::n]

    def radius(x_c, y_c, x_1, y_1, x_2, y_2):

        k1 = ((x_c + x_1) / 2 - x_c) / ((y_c + y_1) / 2 - y_c)
        b1 = (y_c + y_1) / 2 + ((x_c + x_1) / 2 - x_c) / ((y_c + y_1) / 2 - y_c) * (x_c + x_1) / 2

        k2 = ((x_c + x_2) / 2 - x_c) / ((y_c + y_2) / 2 - y_c)
        b2 = (y_c + y_2) / 2 + k2 * (x_c + x_2) / 2

        x_r = (b2 - b1) / (k2 - k1)
        y_r = -k1 * x_r + b1

        rad = np.sqrt((x_r - x_c) ** 2 + (y_r - y_c) ** 2)
        return rad, x_r, y_r

    x = np.array(time)
    y = np.array(voltage)

    l:int = 32
    dff = flat(x, y, l)
    ma:float = nearest_value(dff[0], dff[1], x[np.argmax(y)], np.min(y))

    o:int = 10

    # очень по гейски но что имеем
    while ma + o >= len(dff[0]):
        l = l // 2
        if l == 0:  # проверка на ноль и замена нуля на 1
            return 10, -10, 0

        dff = flat(x, y, l)
        ma:float = nearest_value(dff[0], dff[1], x[np.argmax(y)], np.min(y))

    while dff[1][ma + o] - dff[1][ma] >= 8:
        if (dff[1][ma + o] - dff[1][ma]) / (dff[1][ma + (o - 1)] - dff[1][ma]) > 3.5:
            o -= 1
            ma = nearest_value(dff[0], dff[1], x[np.argmax(y)], np.min(y))
            break
        o -= 1
        ma:float = nearest_value(dff[0], dff[1], x[np.argmax(y)], np.min(y))

    rad, x_r, y_r = radius(dff[0][ma], dff[1][ma], dff[0][ma + o], dff[1][ma + o], dff[0][ma - o], dff[1][ma - o])
    return rad, x_r, y_r